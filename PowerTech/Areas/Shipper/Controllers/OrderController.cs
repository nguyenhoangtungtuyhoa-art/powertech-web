using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Hubs;
using PowerTech.Models.Entities;

namespace PowerTech.Areas.Shipper.Controllers
{
    [Area("Shipper")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrderController(ApplicationDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // Danh sách đơn hàng cần giao
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Where(o => o.OrderStatus == "Shipping" || o.OrderStatus == "Shipped" || o.OrderStatus == "Processing")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
                
            return View(orders);
        }

        // Chi tiết đơn hàng cho Shipper
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.User)
                .Include(o => o.OrderHistories.OrderByDescending(h => h.CreatedAt))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // Xác nhận giao hàng thành công & Thanh toán COD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(int id)
        {
            try 
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null) return NotFound();

                // Cập nhật trạng thái đơn hàng
                order.OrderStatus = "Completed";
                order.UpdatedAt = DateTime.Now;

                // Nếu là đơn COD hoặc chưa thanh toán, xác nhận đã nhận tiền
                if (order.PaymentStatus != "Paid")
                {
                    order.PaymentStatus = "Paid";
                }

                _context.Update(order);

                // Lưu lịch sử
                var history = new OrderHistory
                {
                    OrderId = order.Id,
                    Status = order.OrderStatus,
                    Action = "Shipper xác nhận giao hàng",
                    Note = "Giao hàng thành công & Thu tiền COD",
                    PerformedBy = "Shipper: " + (User.Identity.Name ?? "System"),
                    CreatedAt = DateTime.Now
                };
                _context.OrderHistories.Add(history);

                await _context.SaveChangesAsync();
                
                // Real-time broadcast: Thông báo cho Admin & Sales
                await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", order.Id, order.OrderStatus, order.PaymentStatus);
                
                // Thông báo riêng cho Sales/Admin
                await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", 
                    "THANH TOÁN THÀNH CÔNG", 
                    $"Đơn hàng #{order.OrderCode} đã được Shipper thu tiền và thanh toán xong.", 
                    "success");

                TempData["Success"] = "Xác nhận giao hàng và thanh toán thành công!";
            }
            catch (Exception ex)
            {
                // Trả về lỗi chi tiết để Shipper biết
                TempData["Error"] = "Lỗi lưu Database: " + (ex.InnerException?.Message ?? ex.Message);
            }
            
            return RedirectToAction(nameof(Index));
        }

        // Báo cáo sự cố/Giao thất bại - Quy tắc 3 lần tự động hủy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportIssue(int id, string reason)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null) return NotFound();

                // Sử dụng InternalNote thay vì Note để tránh lỗi vượt quá 500 ký tự
                string internalNote = order.InternalNote ?? "";
                
                // Nếu khách từ chối nhận -> Hủy ngay lập tức
                if (reason == "Khách từ chối nhận hàng")
                {
                    order.OrderStatus = "Cancelled";
                    order.InternalNote = (order.InternalNote ?? "") + $" | [KHÁCH TỪ CHỐI]: {reason}";
                    
                    await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", 
                        "ĐƠN HÀNG BỊ HỦY", 
                        $"Đơn hàng #{order.OrderCode} bị khách từ chối nhận!", 
                        "error");
                }
                else 
                {
                    order.DeliveryFailCount++;

                    if (order.DeliveryFailCount >= 3)
                    {
                        order.OrderStatus = "Cancelled";
                        order.InternalNote = (order.InternalNote ?? "") + $" | [GIAO THẤT BẠI LẦN 3]: {reason} -> TỰ ĐỘNG HỦY ĐƠN";
                        
                        await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", 
                            "ĐƠN HÀNG BỊ HỦY", 
                            $"Đơn hàng #{order.OrderCode} bị hủy do giao thất bại 3 lần!", 
                            "error");
                    }
                    else
                    {
                        order.OrderStatus = "Processing"; 
                        order.InternalNote = (order.InternalNote ?? "") + $" | [GIAO THẤT BẠI LẦN {order.DeliveryFailCount}]: {reason}";
                    }
                }

                order.UpdatedAt = DateTime.Now;
                _context.Update(order);

                // Lưu lịch sử
                var history = new OrderHistory
                {
                    OrderId = order.Id,
                    Status = order.OrderStatus,
                    Action = "Shipper báo cáo sự cố",
                    Note = reason,
                    PerformedBy = "Shipper: " + (User.Identity.Name ?? "System"),
                    CreatedAt = DateTime.Now
                };
                _context.OrderHistories.Add(history);

                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", order.Id, order.OrderStatus, order.PaymentStatus);
                
                TempData["Warning"] = order.OrderStatus == "Cancelled" ? "Đơn hàng đã được chuyển sang mục Lịch sử (Hủy)." : "Đã ghi nhận sự cố.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi lưu dữ liệu: " + ex.Message;
            }
            
            return RedirectToAction(nameof(Index));
        }

        // LỊCH SỬ: Xem các đơn đã hoàn tất hoặc đã hủy
        public async Task<IActionResult> History()
        {
            var history = await _context.Orders
                .Where(o => o.OrderStatus == "Completed" || o.OrderStatus == "Cancelled")
                .OrderByDescending(o => o.UpdatedAt)
                .ToListAsync();
            return View(history);
        }

        // VÍ TIỀN: Giả lập thu nhập
        public async Task<IActionResult> Wallet()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Lấy danh sách đơn hàng đã hoàn thành và đã thanh toán của shipper này
            var completedOrders = await _context.Orders
                .Where(o => o.OrderStatus == "Completed" && o.PaymentStatus == "Paid")
                .OrderByDescending(o => o.UpdatedAt)
                .Take(5)
                .ToListAsync();

            // Tính thu nhập hôm nay
            var today = DateTime.Today;
            var todayIncome = await _context.Orders
                .Where(o => o.OrderStatus == "Completed" && o.PaymentStatus == "Paid" && o.UpdatedAt >= today)
                .SumAsync(o => o.TotalAmount);
            
            var todayCount = await _context.Orders
                .Where(o => o.OrderStatus == "Completed" && o.PaymentStatus == "Paid" && o.UpdatedAt >= today)
                .CountAsync();

            ViewBag.DailyIncome = todayIncome;
            ViewBag.DailyCount = todayCount;
            ViewBag.Balance = user.WalletBalance;
            ViewBag.CashInHand = 1500000; 
            
            return View(completedOrders);
        }

        // --- SA-05: Giả lập thanh toán qua QR ---
        
        [HttpGet]
        public async Task<IActionResult> PaySimulation(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPaySimulation(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            if (order.PaymentStatus != "Paid")
            {
                order.PaymentStatus = "Paid";
                order.OrderStatus = "Completed";
                order.UpdatedAt = DateTime.Now;

                // Cộng tiền vào ví của Shipper (người đang xử lý đơn)
                var currentShipper = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
                if (currentShipper != null)
                {
                    currentShipper.WalletBalance += order.TotalAmount;
                    _context.Update(currentShipper);
                }

                _context.Update(order);

                // Lưu lịch sử
                var history = new OrderHistory
                {
                    OrderId = order.Id,
                    Status = order.OrderStatus,
                    Action = "Thanh toán QR thành công",
                    Note = "Khách hàng đã quét mã và xác nhận chuyển khoản",
                    PerformedBy = "Customer via PaySimulation",
                    CreatedAt = DateTime.Now
                };
                _context.OrderHistories.Add(history);

                await _context.SaveChangesAsync();

                // Gửi SignalR thông báo cho trang Details của Shipper tự động load lại hoặc báo thành công
                await _hubContext.Clients.All.SendAsync("ReceiveOrderPaid", order.Id);
                
                await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", 
                    "THANH TOÁN QR", 
                    $"Đơn hàng #{order.OrderCode} đã thanh toán thành công qua QR.", 
                    "success");
            }

            return View("PaySuccess", order);
        }

        // CÀI ĐẶT: Thông tin shipper
        public IActionResult Settings()
        {
            return View();
        }
    }
}
