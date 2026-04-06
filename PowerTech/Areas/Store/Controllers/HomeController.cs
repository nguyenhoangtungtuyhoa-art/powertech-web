using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Models.ViewModels.Store;

namespace PowerTech.Areas.Store.Controllers
{
    [Area("Store")]
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel
            {
                FeaturedCategories = await _context.Categories
                    .Where(c => c.IsActive && c.ParentCategoryId == null)
                    .OrderBy(c => c.DisplayOrder)
                    .Take(8)
                    .ToListAsync(),

                FeaturedProducts = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.IsFeatured)
                    .ThenByDescending(p => p.CreatedAt)
                    .Take(10)
                    .ToListAsync(),

                NewProducts = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(10)
                    .ToListAsync(),

                DiscountProducts = await _context.Products
                    .Where(p => p.IsActive && p.DiscountPrice.HasValue)
                    .OrderByDescending(p => (p.Price - p.DiscountPrice) / p.Price)
                    .Take(10)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        public IActionResult Error(int? statusCode = null)
        {
            if (statusCode == 404)
            {
                return View("NotFound");
            }
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
