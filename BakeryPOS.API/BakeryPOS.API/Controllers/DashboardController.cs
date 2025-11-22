using BakeryPOS.API.Core.Attributes;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/dashboard/summary
        // Returns the data for the 4 main cards at the top of the dashboard
        [HasPermission(UserPermissions.AccessReports)]
        [HttpGet("summary")]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
        {
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            // Calculate start of this week (assuming Monday start)
            int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

            var startOfThisWeek = today.AddDays(-daysSinceMonday);
            var startOfLastWeek = startOfThisWeek.AddDays(-7);

            // --- 1. Sales & Transactions (Today vs Yesterday) ---
            var todaysSalesData = await _context.Sales
                .Where(s => s.SaleDate >= today && s.SaleDate < today.AddDays(1))
                .Select(s => new { s.FinalAmount })
                .ToListAsync();

            var yesterdaysSalesData = await _context.Sales
                .Where(s => s.SaleDate >= yesterday && s.SaleDate < today)
                .Select(s => new { s.FinalAmount })
                .ToListAsync();

            decimal todaysTotal = todaysSalesData.Sum(s => s.FinalAmount);
            decimal yesterdaysTotal = yesterdaysSalesData.Sum(s => s.FinalAmount);
            int todaysCount = todaysSalesData.Count;
            int yesterdaysCount = yesterdaysSalesData.Count;

            // --- 2. New Clients (This Week vs Last Week) ---
            var newClientsThisWeek = await _context.Customers
                .CountAsync(c => c.CreatedAt >= startOfThisWeek);

            var newClientsLastWeek = await _context.Customers
                .CountAsync(c => c.CreatedAt >= startOfLastWeek && c.CreatedAt < startOfThisWeek);

            // --- 3. Discounts (This Week vs Last Week) ---
            var discountsThisWeek = await _context.Sales
                .Where(s => s.SaleDate >= startOfThisWeek)
                .SumAsync(s => s.DiscountAmount);

            var discountsLastWeek = await _context.Sales
                .Where(s => s.SaleDate >= startOfLastWeek && s.SaleDate < startOfThisWeek)
                .SumAsync(s => s.DiscountAmount);

            // --- 4. Total Clients & Discount Transactions ---
            // Count all customers in the database
            var totalClients = await _context.Customers.CountAsync();

            // Count all sales where a discount was applied (DiscountAmount > 0)
            var totalDiscountTransactions = await _context.Sales.CountAsync(s => s.DiscountAmount > 0);


            // --- Build DTO ---
            var summary = new DashboardSummaryDto
            {
                // Sales
                TodaysSales = todaysTotal,
                SalesChangePercentage = CalculatePercentageChange(todaysTotal, yesterdaysTotal),
                TodaysTransactions = todaysCount,

                // Clients
                TotalClients = totalClients,
                NewClientsThisWeek = newClientsThisWeek,
                ClientsChangePercentage = CalculatePercentageChange(newClientsThisWeek, newClientsLastWeek),

                // Discounts
                DiscountsThisWeek = discountsThisWeek,
                TotalDiscountTransactions = totalDiscountTransactions,
                DiscountsChangePercentage = CalculatePercentageChange(discountsThisWeek, discountsLastWeek)

                
                
            };

            return Ok(summary);
        }

        // Helper function to calculate percentage change safely
        private decimal CalculatePercentageChange(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return Math.Round(((current - previous) / previous) * 100, 1);
        }


        // GET: api/dashboard/notifications
        // Returns alerts for low stock and pending payments
        [HasPermission(UserPermissions.AccessReports)]
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = new List<object>();

            // 1. Low Stock Alerts (e.g., less than 10 items)
            var lowStockProducts = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity < 10)
                .Select(p => new { p.Name, p.StockQuantity })
                .ToListAsync();

            foreach (var p in lowStockProducts)
            {
                notifications.Add(new
                {
                    type = "Stock limité",
                    message = $"{p.Name} est presque épuisé ({p.StockQuantity} restants).",
                    severity = "warning",
                    time = DateTime.UtcNow
                });
            }

            // 2. Pending Payments Alerts (Customers who owe money)
            var debtCustomers = await _context.Customers
                .Where(c => c.CurrentBalance < 0) // Negative balance = debt
                .Select(c => new { c.Name, c.CurrentBalance })
                .ToListAsync();

            foreach (var c in debtCustomers)
            {
                notifications.Add(new
                {
                    type = "Paiement en attente",
                    message = $"{c.Name} a un solde impayé de {Math.Abs(c.CurrentBalance):C}.",
                    severity = "danger",
                    time = DateTime.UtcNow
                });
            }

            return Ok(notifications);
        }


        // GET: api/dashboard/topselling
        [HasPermission(UserPermissions.AccessReports)]
        [HttpGet("topselling")]
        public async Task<ActionResult<IEnumerable<TopSellingProductDto>>> GetTopSellingProducts([FromQuery] int count = 5)
        {
            // 1. Define Periods
            var thisMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);

            // 2. Get Top Products for THIS Month
            var topProducts = await _context.SaleDetails
                .Where(sd => sd.Sale.SaleDate >= thisMonthStart)
                .GroupBy(sd => new { sd.ProductId, sd.Product.Name })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.Name,
                    Revenue = g.Sum(sd => sd.Quantity * sd.UnitPrice),
                    Sold = g.Sum(sd => sd.Quantity)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(count)
                .ToListAsync();

            // 3. Get Revenue for the SAME products in LAST Month
            var topProductIds = topProducts.Select(p => p.ProductId).ToList();

            var lastMonthRevenues = await _context.SaleDetails
                .Where(sd => sd.Sale.SaleDate >= lastMonthStart && sd.Sale.SaleDate < thisMonthStart)
                .Where(sd => topProductIds.Contains(sd.ProductId))
                .GroupBy(sd => sd.ProductId)
                .Select(g => new { ProductId = g.Key, Revenue = g.Sum(sd => sd.Quantity * sd.UnitPrice) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Revenue);

            // 4. Merge and Calculate Growth
            var result = topProducts.Select(p =>
            {
                decimal lastMonthRev = lastMonthRevenues.ContainsKey(p.ProductId) ? lastMonthRevenues[p.ProductId] : 0;
                return new TopSellingProductDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.Name,
                    TotalSold = p.Sold,
                    TotalRevenue = p.Revenue,
                    GrowthPercentage = CalculatePercentageChange(p.Revenue, lastMonthRev) // Uses the helper method we made earlier
                };
            });

            return Ok(result);
        }


        // GET: api/dashboard/salesovertime?period=week
        [HasPermission(UserPermissions.AccessReports)]
        [HttpGet("salesovertime")]
        public async Task<ActionResult<IEnumerable<SalesDataPointDto>>> GetSalesOverTime([FromQuery] string period = "week")
        {
            List<SalesDataPointDto> salesData;
            var today = DateTime.UtcNow.Date;

            switch (period.ToLower())
            {
                case "year":
                    var yearlyData = await _context.Sales
                        .Where(s => s.SaleDate >= today.AddYears(-1))
                        .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                        .Select(group => new
                        {
                            Year = group.Key.Year,
                            Month = group.Key.Month,
                            TotalSales = group.Sum(s => s.FinalAmount)
                        })
                        .ToListAsync();

                    salesData = yearlyData.Select(s => new SalesDataPointDto
                    {
                        Label = new DateTime(s.Year, s.Month, 1).ToString("MMM yyyy"),
                        TotalSales = s.TotalSales
                    })
                    .OrderBy(s => DateTime.Parse(s.Label))
                    .ToList();
                    break;

                case "month":
                    var monthlyData = await _context.Sales
                        .Where(s => s.SaleDate >= today.AddDays(-30))
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(group => new
                        {
                            Date = group.Key,
                            TotalSales = group.Sum(s => s.FinalAmount)
                        })
                        .ToListAsync();

                    salesData = monthlyData.Select(s => new SalesDataPointDto
                    {
                        Label = s.Date.ToString("yyyy-MM-dd"),
                        TotalSales = s.TotalSales
                    })
                    .OrderBy(s => s.Label)
                    .ToList();
                    break;

                case "week":
                default:
                    var weeklyData = await _context.Sales
                        .Where(s => s.SaleDate >= today.AddDays(-7))
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(group => new
                        {
                            Date = group.Key,
                            TotalSales = group.Sum(s => s.FinalAmount)
                        })
                        .ToListAsync();

                    salesData = weeklyData.Select(s => new SalesDataPointDto
                    {
                        Label = s.Date.DayOfWeek.ToString(),
                        TotalSales = s.TotalSales
                    })
                    .OrderBy(s => s.Label) // Note: Sorting day names alphabetically isn't ideal, better to sort by Date in frontend
                    .ToList();
                    break;
            }

            return Ok(salesData);
        }

        // GET: api/dashboard/cashierperformance
        [HasPermission(UserPermissions.AccessReports)]
        [HttpGet("cashierperformance")]
        public async Task<ActionResult<IEnumerable<CashierPerformanceDto>>> GetCashierPerformance([FromQuery] string period = "today")
        {
            var startDate = DateTime.UtcNow.Date;
            if (period.ToLower() == "week") startDate = DateTime.UtcNow.Date.AddDays(-7);
            else if (period.ToLower() == "month") startDate = DateTime.UtcNow.Date.AddDays(-30);

            var performanceData = await _context.Sales
                .Include(s => s.User)
                .Where(s => s.SaleDate >= startDate)
                .GroupBy(s => new { s.UserId, s.User.FullName })
                .Select(group => new CashierPerformanceDto
                {
                    UserId = group.Key.UserId,
                    CashierName = group.Key.FullName,
                    TotalTransactions = group.Count(),
                    TotalSalesValue = group.Sum(s => s.FinalAmount)
                })
                .OrderByDescending(p => p.TotalSalesValue)
                .ToListAsync();

            return Ok(performanceData);
        }

        // GET: api/dashboard/topclients
        [HasPermission(UserPermissions.AccessReports)]
        [HttpGet("topclients")]
        public async Task<ActionResult<IEnumerable<TopClientDto>>> GetTopClients([FromQuery] int count = 5)
        {
            var topClients = await _context.Customers
                .Select(c => new TopClientDto
                {
                    CustomerId = c.Id,
                    CustomerName = c.Name,
                    TotalSpent = c.Sales.Sum(s => s.FinalAmount),
                    TotalOrders = c.Sales.Count(),
                    OutstandingBalance = c.CurrentBalance < 0 ? c.CurrentBalance * -1 : 0
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(count)
                .ToListAsync();

            return Ok(topClients);
        }
    }
}