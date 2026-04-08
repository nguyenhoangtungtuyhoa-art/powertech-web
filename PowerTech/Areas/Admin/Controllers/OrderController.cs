using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PowerTech.Constants;
using PowerTech.Data;
using PowerTech.Hubs;
using PowerTech.Models.Entities;

namespace PowerTech.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = UserRoles.Admin)]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status, string? q)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(o => o.OrderCode.Contains(q) || o.ReceiverName.Contains(q) || o.PhoneNumber.Contains(q));
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.Query = q;
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .Include(o => o.OrderHistories.OrderByDescending(h => h.CreatedAt))
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            // Quy tắc 1: Luồng trạng thái 1 chiều (One-way flow)
            Dictionary<string, int> statusWeight = new() {
                { "Pending", 0 },
                { "Processing", 1 },
                { "Shipping", 2 },
                { "Shipped", 2 },
                { "Completed", 3 },
                { "Cancelled", -1 }
            };

            int currentWeight = statusWeight.ContainsKey(order.OrderStatus) ? statusWeight[order.OrderStatus] : 0;
            int nextWeight = statusWeight.ContainsKey(status) ? statusWeight[status] : 0;

            if (nextWeight != -1 && nextWeight <= currentWeight)
            {
                TempData["Error"] = "Cần tuân thủ quy trình: Chờ xác nhận -> Đang xử lý -> Đang giao hàng -> Hoàn tất. Không thể quay lại trạng thái trước!";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            // Quy tắc 2: Chỉ Hoàn tất khi đã Thanh toán (Paid)
            if (status == "Completed" && order.PaymentStatus != "Paid")
            {
                TempData["Error"] = "Đơn hàng chỉ có thể HOÀN TẤT sau khi bên Shipper hoặc Hệ thống xác nhận ĐÃ THANH TOÁN!";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            order.OrderStatus = status;
            order.UpdatedAt = DateTime.Now;

            _context.Update(order);

            // Lưu lịch sử
            var history = new OrderHistory
            {
                OrderId = order.Id,
                Status = status,
                Action = "Hệ thống cập nhật trạng thái",
                Note = $"Chuyển trạng thái sang: {status}",
                PerformedBy = "Admin/Staff: " + (User.Identity.Name ?? "System"),
                CreatedAt = DateTime.Now
            };
            _context.OrderHistories.Add(history);

            await _context.SaveChangesAsync();

            // Real-time notification (SignalR)
            var hubContext = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<PowerTech.Hubs.OrderHub>>();
            await hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", order.Id, status, order.PaymentStatus);

            TempData["Success"] = $"Đã chuyển trạng thái đơn hàng sang: {status}";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
    }
}
