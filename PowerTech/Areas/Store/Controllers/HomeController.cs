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
                    .ToListAsync(),

                FeaturedProducts = await _context.Products
                    .Include(p => p.ProductSpecifications)
                    .Where(p => p.IsActive)
                    .OrderBy(p => Guid.NewGuid()) 
                    .Take(15) 
                    .ToListAsync(),

                NewProducts = await _context.Products
                    .Include(p => p.ProductSpecifications)
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(20)
                    .OrderBy(p => Guid.NewGuid()) 
                    .Take(15)
                    .ToListAsync(),

                DiscountProducts = await _context.Products
                    .Include(p => p.ProductSpecifications)
                    .Where(p => p.IsActive && p.DiscountPrice.HasValue)
                    .OrderByDescending(p => (p.Price - p.DiscountPrice) / p.Price)
                    .Take(20)
                    .OrderBy(p => Guid.NewGuid()) 
                    .Take(15)
                    .ToListAsync(),

                TopBrands = await _context.Brands
                    .OrderBy(b => Guid.NewGuid())
                    .Take(12)
                    .ToListAsync(),

                CategorySections = await _context.Categories
                    .Where(c => c.IsActive && c.ParentCategoryId == null)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new CategorySection
                    {
                        Category = c,
                        Products = _context.Products
                            .Include(p => p.ProductSpecifications)
                            .Where(p => p.IsActive && (p.CategoryId == c.Id || p.Category.ParentCategoryId == c.Id))
                            .OrderBy(p => Guid.NewGuid()) 
                            .Take(10)
                            .ToList()
                    })
                    .Where(cs => cs.Products.Any())
                    .ToListAsync(),

                MenuCategories = await _context.Categories
                    .Where(c => c.IsActive && c.ParentCategoryId == null)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new MenuCategory
                    {
                        Category = c,
                        Children = _context.Categories
                            .Where(child => child.ParentCategoryId == c.Id && child.IsActive)
                            .OrderBy(child => child.DisplayOrder)
                            .ToList(),
                        Brands = _context.Products
                            .Where(p => p.IsActive && (p.CategoryId == c.Id || p.Category.ParentCategoryId == c.Id))
                            .Select(p => p.Brand)
                            .Distinct()
                            .Take(8)
                            .ToList()
                    })
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
    }
}
