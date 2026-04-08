using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PowerTech.Models.Entities;
using PowerTech.Services.Interfaces;

namespace PowerTech.Areas.Store.Controllers
{
    [Area("Store")]
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly UserManager<ApplicationUser> _userManager;
        private const string ANONYMOUS_CART_COOKIE = "PT_GuestCartId";

        public CartController(ICartService cartService, UserManager<ApplicationUser> userManager)
        {
            _cartService = cartService;
            _userManager = userManager;
        }

        private async Task<string> GetCartOwnerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                return user.Id;
            }

            // Dùng AnonymousId từ Cookie
            if (Request.Cookies.ContainsKey(ANONYMOUS_CART_COOKIE))
            {
                return Request.Cookies[ANONYMOUS_CART_COOKIE]!;
            }

            // Tạo mới nếu chưa có
            var guestId = Guid.NewGuid().ToString();
            var options = new CookieOptions { 
                Expires = DateTime.Now.AddDays(30), 
                HttpOnly = true, 
                IsEssential = true 
            };
            Response.Cookies.Append(ANONYMOUS_CART_COOKIE, guestId, options);
            return guestId;
        }

        public async Task<IActionResult> Index()
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var cart = await _cartService.GetCartAsync(cartOwnerId);
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var result = await _cartService.AddToCartAsync(cartOwnerId, productId, quantity);
            
            if (result == -1) return Json(new { success = false, message = "Sản phẩm không tồn tại!" });
            if (result == -2) return Json(new { success = false, message = "Số lượng vượt quá sản phẩm hiện có trong kho!" });

            // Lưu vào Cookie để Layout đọc tức thì
            Response.Cookies.Append("PT_CartCount", result.ToString(), new CookieOptions { 
                Expires = DateTime.Now.AddDays(30), 
                HttpOnly = false, // Cho phép JS đọc
                IsEssential = true,
                Path = "/"
            });

            return Json(new { success = true, count = result, message = "Đã thêm vào giỏ hàng thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var success = await _cartService.UpdateQuantityAsync(cartOwnerId, productId, quantity);
            
            if (!success)
            {
                return Json(new { success = false, message = "Số lượng vượt quá tồn kho!" });
            }

            var cart = await _cartService.GetCartAsync(cartOwnerId);
            
            // Cập nhật Cookie số lượng mới
            var newCount = cart.CartItems.Sum(ci => ci.Quantity);
            Response.Cookies.Append("PT_CartCount", newCount.ToString(), new CookieOptions { 
                Expires = DateTime.Now.AddDays(30), 
                HttpOnly = false,
                IsEssential = true,
                Path = "/"
            });

            return Json(new { 
                success = true, 
                count = newCount,
                total = cart.CartItems.Sum(ci => ci.Quantity * ci.UnitPrice).ToString("N0") + "₫",
                itemTotal = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId)?.Quantity * cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId)?.UnitPrice
            });
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int productId)
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var success = await _cartService.RemoveFromCartAsync(cartOwnerId, productId);
            
            // Lấy lại số lượng mới sau khi xóa
            var count = await _cartService.GetCartItemCountAsync(cartOwnerId);
            Response.Cookies.Append("PT_CartCount", count.ToString(), new CookieOptions { 
                Expires = DateTime.Now.AddDays(30), 
                HttpOnly = false,
                IsEssential = true,
                Path = "/"
            });

            return Json(new { success = success, count = count });
        }

        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var count = await _cartService.GetCartItemCountAsync(cartOwnerId);
            
            // Lưu vào Cookie để Layout đọc tức thì
            Response.Cookies.Append("PT_CartCount", count.ToString(), new CookieOptions { 
                Expires = DateTime.Now.AddDays(30), 
                HttpOnly = false,
                IsEssential = true,
                Path = "/"
            });

            return Json(count);
        }
    }
}
