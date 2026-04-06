using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Constants;

namespace PowerTech.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize(Roles = UserRoles.SalesStaff + "," + UserRoles.Admin)]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var stats = new
            {
                PendingOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Pending"),
                ProcessingOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Processing" || o.OrderStatus == "Confirmed"),
                ShippedOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Shipped"),
                CompletedOrdersMonth = await _context.Orders.CountAsync(o => o.CreatedAt >= startOfMonth && o.OrderStatus == "Delivered"),
                RevenueToday = await _context.Orders
                    .Where(o => o.CreatedAt >= today && o.PaymentStatus == "Paid")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0,
                RecentOrders = await _context.Orders
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .Include(o => o.User)
                    .ToListAsync(),
                TopProducts = await _context.OrderItems
                    .GroupBy(oi => new { oi.ProductId, oi.ProductNameSnapshot })
                    .Select(g => new {
                        Name = g.Key.ProductNameSnapshot,
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.LineTotal)
                    })
                    .OrderByDescending(x => x.Quantity)
                    .Take(5)
                    .ToListAsync()
            };

            ViewBag.Stats = stats;
            return View();
        }
    }
}
