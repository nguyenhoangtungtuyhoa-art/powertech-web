using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Constants;
using PowerTech.Models.Entities;

namespace PowerTech.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = UserRoles.WarehouseStaff + "," + UserRoles.Admin)]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int LowStockThreshold = 10;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchTerm, int? categoryId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .OrderBy(p => p.StockQuantity)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm) || p.SKU.Contains(searchTerm));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            var products = await query.ToListAsync();
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CategoryId = categoryId;
            ViewBag.LowStockThreshold = LowStockThreshold;

            return View(products);
        }

        public async Task<IActionResult> LowStock()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockQuantity <= LowStockThreshold)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            ViewBag.LowStockThreshold = LowStockThreshold;
            return View("Index", products);
        }

        public async Task<IActionResult> History()
        {
            var transactions = await _context.StockTransactions
                .Include(st => st.Product)
                .Include(st => st.PerformedByUser)
                .OrderByDescending(st => st.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(transactions);
        }
    }
}
