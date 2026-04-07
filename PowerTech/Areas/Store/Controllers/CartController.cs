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
            var count = await _cartService.AddToCartAsync(cartOwnerId, productId, quantity);
            return Json(new { success = true, count = count, message = "Đã thêm vào giỏ hàng thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var success = await _cartService.UpdateQuantityAsync(cartOwnerId, productId, quantity);
            var cart = await _cartService.GetCartAsync(cartOwnerId);
            
            return Json(new { 
                success = success, 
                total = cart.CartItems.Sum(ci => ci.Quantity * ci.UnitPrice).ToString("N0") + "₫",
                itemTotal = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId)?.Quantity * cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId)?.UnitPrice
            });
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int productId)
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var success = await _cartService.RemoveFromCartAsync(cartOwnerId, productId);
            return Json(new { success = success });
        }

        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var cartOwnerId = await GetCartOwnerIdAsync();
            var count = await _cartService.GetCartItemCountAsync(cartOwnerId);
            return Json(count);
        }
    }
}
