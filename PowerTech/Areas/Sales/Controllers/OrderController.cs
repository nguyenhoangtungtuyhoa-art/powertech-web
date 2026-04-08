using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Constants;
using PowerTech.Models.Entities;

namespace PowerTech.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize(Roles = UserRoles.SalesStaff + "," + UserRoles.Admin)]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status, string? searchTerm)
        {
            var ordersQuery = _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                ordersQuery = ordersQuery.Where(o => o.OrderStatus == status);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                ordersQuery = ordersQuery.Where(o => 
                    o.OrderCode.Contains(searchTerm) || 
                    o.ReceiverName.Contains(searchTerm) || 
                    o.PhoneNumber.Contains(searchTerm));
            }

            var orders = await ordersQuery.ToListAsync();
            ViewBag.CurrentStatus = status;
            ViewBag.SearchTerm = searchTerm;
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, string status, string? internalNote)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            order.OrderStatus = status;
            order.InternalNote = internalNote;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Cập nhật đơn hàng {order.OrderCode} thành công!";
            
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        // --- SA-04: Hỗ trợ tạo đơn tại quầy ---
        
        public IActionResult Create()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> SearchProducts(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new List<object>());

            var products = await _context.Products
                .Where(p => p.IsActive && (p.Name.Contains(term) || p.SKU.Contains(term)))
                .Take(10)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.Price,
                    p.StockQuantity,
                    p.ThumbnailUrl
                })
                .ToListAsync();

            return Json(products);
        }

        [HttpGet]
        public async Task<JsonResult> SearchUsers(string term)
        {
            if (string.IsNullOrEmpty(term)) return Json(new List<object>());

            var users = await _context.Users
                .Where(u => (u.Email != null && u.Email.Contains(term)) || 
                            (u.PhoneNumber != null && u.PhoneNumber.Contains(term)) || 
                            (u.FullName != null && u.FullName.Contains(term)))
                .Take(5)
                .Select(u => new {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber
                })
                .ToListAsync();

            return Json(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string userId, string receiverName, string phoneNumber, string shippingAddress, string paymentMethod, string? note, string orderItemsJson)
        {
            // Simple validation
            if (string.IsNullOrEmpty(orderItemsJson))
            {
                ModelState.AddModelError("", "Đơn hàng phải có ít nhất một sản phẩm.");
                return View();
            }

            var orderItems = System.Text.Json.JsonSerializer.Deserialize<List<OrderItemSubmitModel>>(orderItemsJson);
            if (orderItems == null || !orderItems.Any())
            {
                ModelState.AddModelError("", "Dữ liệu sản phẩm không hợp lệ.");
                return View();
            }

            // If userId is empty, we might need a default 'Guest' user ID.
            // For now, let's assume the user must exist or we find the Admin as the owner if it's a walk-in.
            // BETTER: Use a constant or find a user with role 'Customer' named 'Guest'.
            if (string.IsNullOrEmpty(userId))
            {
                var guestUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == "guest" || u.Email == "guest@powertech.com");
                if (guestUser != null) userId = guestUser.Id;
                else
                {
                    // Fallback to current admin if guest not found
                    var currentUserName = User.Identity?.Name;
                    var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == currentUserName);
                    userId = currentUser?.Id ?? throw new Exception("Owner user not found");
                }
            }

            decimal totalAmount = 0;
            var finalOrderItems = new List<OrderItem>();

            foreach (var item in orderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null) continue;

                var quantity = item.Quantity > 0 ? item.Quantity : 1;
                var unitPrice = product.Price; // Use current price
                var lineTotal = quantity * unitPrice;

                finalOrderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductNameSnapshot = product.Name,
                    ProductSkuSnapshot = product.SKU,
                    ProductImageSnapshot = product.ThumbnailUrl,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal
                });

                totalAmount += lineTotal;
                
                // Update stock
                product.StockQuantity -= quantity;
                product.SoldQuantity += quantity;
            }

            var order = new Order
            {
                OrderCode = $"POS-{DateTime.UtcNow:yyyyMMddHHmm}-{new Random().Next(100, 999)}",
                UserId = userId,
                ReceiverName = receiverName,
                PhoneNumber = phoneNumber,
                ShippingAddress = string.IsNullOrEmpty(shippingAddress) ? "Tại quầy" : shippingAddress,
                OrderStatus = "Completed", // POS orders are typically delivered immediately
                PaymentStatus = "Paid",     // POS orders are typically paid immediately
                PaymentMethod = paymentMethod,
                Subtotal = totalAmount,
                ShippingFee = 0,
                DiscountAmount = 0,
                TotalAmount = totalAmount,
                Note = note,
                InternalNote = "Đơn hàng tạo tại quầy bởi Sales",
                CreatedAt = DateTime.UtcNow
            };

            foreach (var oi in finalOrderItems)
            {
                order.OrderItems.Add(oi);
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo đơn hàng {order.OrderCode} thành công!";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        public class OrderItemSubmitModel
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }
    }
}
