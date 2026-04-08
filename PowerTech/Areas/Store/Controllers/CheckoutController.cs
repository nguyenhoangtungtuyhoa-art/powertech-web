using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Models.Entities;
using PowerTech.Services.Interfaces;

namespace PowerTech.Areas.Store.Controllers
{
    [Area("Store")]
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICartService _cartService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckoutController(ApplicationDbContext context, ICartService cartService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _cartService = cartService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? productIds)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var cart = await _cartService.GetCartAsync(user.Id);

            if (cart.CartItems == null || !cart.CartItems.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            // Nếu có productIds, lọc giỏ hàng
            if (!string.IsNullOrEmpty(productIds))
            {
                var idList = productIds.Split(',').Select(int.Parse).ToList();
                cart.CartItems = cart.CartItems.Where(ci => idList.Contains(ci.ProductId)).ToList();
                TempData["CheckoutProductIds"] = productIds;
            }
            else
            {
                // Nếu không có productIds (truy cập trực tiếp), mặc định dọn đường về giỏ hàng hoặc chọn tất cả
                // Ở đây ta chọn tất cả IDs hiện có trong cart
                var allIds = string.Join(",", cart.CartItems.Select(ci => ci.ProductId));
                TempData["CheckoutProductIds"] = allIds;
            }

            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            ViewBag.Cart = cart;
            return View(addresses);
        }

        [HttpPost]
        public IActionResult UpdateAddress(int addressId)
        {
            TempData["SelectedAddressId"] = addressId;
            return RedirectToAction("Payment");
        }

        public async Task<IActionResult> Payment()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var cart = await _cartService.GetCartAsync(user.Id);
            
            var productIds = TempData.Peek("CheckoutProductIds") as string;
            if (!string.IsNullOrEmpty(productIds))
            {
                var idList = productIds.Split(',').Select(int.Parse).ToList();
                cart.CartItems = cart.CartItems.Where(ci => idList.Contains(ci.ProductId)).ToList();
            }

            var addressId = TempData.Peek("SelectedAddressId");
            if (addressId == null) return RedirectToAction("Index");
            
            ViewBag.Cart = cart;
            ViewBag.Address = await _context.UserAddresses.FindAsync(addressId);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string paymentMethod, string note)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var cart = await _cartService.GetCartAsync(user.Id);
            var addressId = (int?)TempData["SelectedAddressId"];
            var productIdsStr = TempData["CheckoutProductIds"] as string;

            if (addressId == null || cart.CartItems == null || !cart.CartItems.Any() || string.IsNullOrEmpty(productIdsStr)) 
                return RedirectToAction("Index");

            var selectedIdList = productIdsStr.Split(',').Select(int.Parse).ToList();
            var selectedItems = cart.CartItems.Where(ci => selectedIdList.Contains(ci.ProductId)).ToList();

            if (!selectedItems.Any()) return RedirectToAction("Index");

            var address = await _context.UserAddresses.FindAsync(addressId);
            if (address == null) return RedirectToAction("Index");

            // 1. Verify stock for all items
            foreach (var item in selectedItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || product.StockQuantity < item.Quantity)
                {
                    TempData["Error"] = $"Sản phẩm {product?.Name ?? "đã chọn"} không còn đủ hàng trong kho!";
                    return RedirectToAction("Index", "Cart");
                }
            }
            var totalAmount = selectedItems.Sum(item => item.Quantity * item.UnitPrice);

            // Create Order
            var order = new Order
            {
                OrderCode = $"PT-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                UserId = user.Id,
                ReceiverName = address.ReceiverName,
                PhoneNumber = address.PhoneNumber,
                ShippingAddress = $"{address.StreetAddress}, {address.Ward}, {address.District}, {address.Province}",
                OrderStatus = "Pending",
                PaymentStatus = "Unpaid",
                PaymentMethod = paymentMethod ?? "COD",
                Subtotal = totalAmount,
                ShippingFee = 0,
                DiscountAmount = 0,
                TotalAmount = totalAmount,
                Note = note,
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Lưu lịch sử lần đầu: Đơn hàng được khởi tạo
            var initialHistory = new OrderHistory
            {
                OrderId = order.Id,
                Status = "Pending",
                Action = "Khách hàng tạo đơn hàng",
                Note = "Đơn hàng được khởi tạo thành công qua website.",
                PerformedBy = "Customer: " + user.FullName,
                CreatedAt = DateTime.Now
            };
            _context.OrderHistories.Add(initialHistory);
            await _context.SaveChangesAsync();

            foreach (var item in selectedItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.Quantity * item.UnitPrice,
                    ProductNameSnapshot = item.Product.Name,
                    ProductSkuSnapshot = item.Product.SKU,
                    ProductImageSnapshot = item.Product.ThumbnailUrl
                };
                _context.OrderItems.Add(orderItem);
                
                // Decrement stock and increment sold count
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                    product.SoldQuantity += item.Quantity;
                    _context.Entry(product).State = EntityState.Modified;
                }

                // Xóa sản phẩm đã mua khỏi giỏ hàng
                await _cartService.RemoveFromCartAsync(user.Id, item.ProductId);
            }

            await _context.SaveChangesAsync();

            // Cập nhật Cookie số lượng giỏ hàng sau khi đã xóa các món đã mua
            var newCount = await _cartService.GetCartItemCountAsync(user.Id);
            Response.Cookies.Append("PT_CartCount", newCount.ToString(), new CookieOptions { Expires = DateTimeOffset.Now.AddDays(7) });

            return RedirectToAction("Confirmation", new { orderId = order.Id });
        }

        [HttpGet]
        public IActionResult AddAddress()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress(UserAddress address)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            address.UserId = user.Id;
            address.CreatedAt = DateTime.UtcNow;
            
            // If the user has no addresses, make this one the default
            var hasAnyAddress = await _context.UserAddresses.AnyAsync(a => a.UserId == user.Id);
            if (!hasAnyAddress)
            {
                address.IsDefault = true;
            }

            _context.UserAddresses.Add(address);
            await _context.SaveChangesAsync();

            TempData["SelectedAddressId"] = address.Id;
            return RedirectToAction("Payment");
        }

        public async Task<IActionResult> Confirmation(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);
                
            return View(order);
        }
    }
}
