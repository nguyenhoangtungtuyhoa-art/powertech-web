using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Constants;
using PowerTech.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace PowerTech.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = UserRoles.WarehouseStaff + "," + UserRoles.Admin)]
    public class StockEntryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StockEntryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToListAsync();
            return View(products);
        }

        public async Task<IActionResult> Create(int? productId)
        {
            if (productId.HasValue)
            {
                var product = await _context.Products.FindAsync(productId.Value);
                if (product != null)
                {
                    ViewBag.SelectedProduct = product;
                }
            }

            ViewBag.Products = await _context.Products.OrderBy(p => p.Name).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int productId, int quantity, string? note)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            if (quantity <= 0)
            {
                ModelState.AddModelError("quantity", "Số lượng nhập phải lớn hơn 0");
                ViewBag.Products = await _context.Products.OrderBy(p => p.Name).ToListAsync();
                ViewBag.SelectedProduct = product;
                return View();
            }

            // Log Audit Trail
            var user = await _userManager.GetUserAsync(User);
            var beforeQty = product.StockQuantity;
            
            // Update Stock
            product.StockQuantity += quantity;
            product.UpdatedAt = DateTime.UtcNow;

            var transaction = new StockTransaction
            {
                ProductId = product.Id,
                PerformedByUserId = user?.Id ?? "Unknown",
                TransactionType = "IMPORT",
                Quantity = quantity,
                ReferenceType = "PurchaseReceipt",
                BeforeQuantity = beforeQty,
                AfterQuantity = product.StockQuantity,
                Note = note ?? "Nhập kho thủ công",
                CreatedAt = DateTime.UtcNow
            };

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Đăng ký nhập kho thành công: {product.Name} (+{quantity})";
            return RedirectToAction("Index", "Inventory");
        }
    }
}
