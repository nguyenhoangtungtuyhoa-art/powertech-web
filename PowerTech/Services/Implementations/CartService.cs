using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Models.Entities;
using PowerTech.Services.Interfaces;

namespace PowerTech.Services.Implementations
{
    public class CartService : ICartService
    {
        private readonly ApplicationDbContext _context;

        public CartService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Cart> GetCartAsync(string ownerId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.UserId == ownerId || c.CookieId == ownerId);

            if (cart == null)
            {
                var isUser = await _context.Users.AnyAsync(u => u.Id == ownerId);
                cart = new Cart();

                if (isUser)
                {
                    cart.UserId = ownerId;
                }
                else
                {
                    cart.CookieId = ownerId;
                }

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        public async Task<int> AddToCartAsync(string userId, int productId, int quantity)
        {
            var cart = await GetCartAsync(userId);
            var product = await _context.Products.FindAsync(productId);

            if (product == null) return -1; // Product not found

            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            var currentInCart = cartItem?.Quantity ?? 0;

            if (currentInCart + quantity > product.StockQuantity)
            {
                // Cannot add more than available stock
                return -2; // Insufficient stock
            }

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
                _context.Entry(cartItem).State = EntityState.Modified;
            }
            else
            {
                cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.DiscountPrice ?? product.Price
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            return cart.CartItems.Sum(ci => ci.Quantity);
        }

        public async Task<bool> RemoveFromCartAsync(string userId, int productId)
        {
            var cart = await GetCartAsync(userId);
            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<bool> UpdateQuantityAsync(string userId, int productId, int quantity)
        {
            if (quantity <= 0) return await RemoveFromCartAsync(userId, productId);

            var cart = await GetCartAsync(userId);
            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            var product = await _context.Products.FindAsync(productId);

            if (cartItem != null && product != null)
            {
                if (quantity > product.StockQuantity) return false; // Stock limit exceeded

                cartItem.Quantity = quantity;
                _context.Entry(cartItem).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<bool> ClearCartAsync(string userId)
        {
            var cart = await GetCartAsync(userId);
            if (cart != null && cart.CartItems.Any())
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<int> GetCartItemCountAsync(string ownerId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == ownerId || c.CookieId == ownerId);
                
            return cart?.CartItems.Sum(ci => ci.Quantity) ?? 0;
        }

        public async Task<decimal> GetCartTotalAsync(string userId)
        {
            var cart = await GetCartAsync(userId);
            return cart.CartItems.Sum(ci => ci.Quantity * ci.UnitPrice);
        }
    }
}
