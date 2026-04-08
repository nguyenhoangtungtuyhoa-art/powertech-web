using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Models.ViewModels.Store;

namespace PowerTech.Areas.Store.Controllers
{
    [Area("Store")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? q, 
            int? categoryId, 
            List<int>? brandIds, 
            decimal? minPrice, 
            decimal? maxPrice, 
            string? sortOrder, 
            int page = 1)
        {
            int pageSize = 15; // Adjusted for 5-per-row grid
            
            var query = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .AsQueryable();

            // Filters
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(p => p.Name.Contains(q) || p.SKU.Contains(q));
            }
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }
            if (brandIds != null && brandIds.Any())
            {
                query = query.Where(p => brandIds.Contains(p.BrandId));
            }
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice);
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice);
            }

            // Sorting
            query = sortOrder switch
            {
                "price_asc" => query.OrderBy(p => p.DiscountPrice ?? p.Price),
                "price_desc" => query.OrderByDescending(p => p.DiscountPrice ?? p.Price),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                "featured" => query.OrderByDescending(p => p.IsFeatured),
                _ => query.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            
            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get relevant brands: brands that have active products in the selected category
            var brandQuery = _context.Brands.Where(b => b.IsActive);
            if (categoryId.HasValue) {
                var brandIdsInCategory = await _context.Products
                    .Where(p => p.CategoryId == categoryId && p.IsActive)
                    .Select(p => p.BrandId)
                    .Distinct()
                    .ToListAsync();
                brandQuery = brandQuery.Where(b => brandIdsInCategory.Contains(b.Id));
            }

            var viewModel = new ProductListViewModel
            {
                Products = products,
                Categories = await _context.Categories.Where(c => c.IsActive && c.ParentCategoryId == null).ToListAsync(),
                Brands = await brandQuery.ToListAsync(),
                
                Query = q,
                CategoryId = categoryId,
                BrandIds = brandIds ?? new List<int>(),
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortOrder = sortOrder,
                
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Detail(string slug)
        {
            var product = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSpecifications)
                    .ThenInclude(ps => ps.SpecificationDefinition)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            // Related products (same category, excluding current)
            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.IsActive)
                .OrderByDescending(p => p.IsFeatured)
                .Take(5)
                .ToListAsync();

            return View(product);
        }
        
        [HttpGet]
        public async Task<IActionResult> Search(string q)
        {
            return await Index(q: q, null, null, null, null, null);
        }
    }
}
