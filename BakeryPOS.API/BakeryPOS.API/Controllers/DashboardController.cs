using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // All dashboard actions should be protected
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/dashboard/summary
        [HttpGet("summary")]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
        {
            var today = DateTime.UtcNow.Date;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek); // Assumes Sunday is the start of the week

            var todaysSales = await _context.Sales
                .Where(s => s.SaleDate >= today && s.SaleDate < today.AddDays(1))
                .ToListAsync();

            var newCustomersThisWeek = await _context.Customers
                .Where(c => c.CreatedAt >= startOfWeek) // Assumes you add a CreatedAt to Customer
                .CountAsync();



            var summary = new DashboardSummaryDto
            {
                TodaysSales = todaysSales.Sum(s => s.FinalAmount),
                TodaysTransactions = todaysSales.Count,
                DiscountsAppliedToday = todaysSales.Sum(s => s.DiscountAmount),
                NewCustomersThisWeek = newCustomersThisWeek
            };

            var yesterdaySales = await _context.Sales
                .Where(s => s.SaleDate >= today.AddDays(-1) && s.SaleDate < today)
                .SumAsync(s => s.FinalAmount);
            var salesChange = CalculatePercentageChange(summary.TodaysSales, yesterdaySales);

            // 2. Clients Comparison (This Week vs Last Week)
            var lastWeekStart = startOfWeek.AddDays(-7);
            var newClientsLastWeek = await _context.Customers
                .Where(c => c.CreatedAt >= lastWeekStart && c.CreatedAt < startOfWeek)
                .CountAsync();
            var clientsChange = CalculatePercentageChange(summary.NewCustomersThisWeek, newClientsLastWeek);

            summary.SalesChangePercentage = salesChange;
            summary.ClientsChangePercentage = clientsChange;

            return Ok(summary);
        }

        // GET: api/dashboard/topselling
        [HttpGet("topselling")]
        public async Task<ActionResult<IEnumerable<TopSellingProductDto>>> GetTopSellingProducts([FromQuery] int count = 5)
        {
            // Query all sale details
            var topProducts = await _context.SaleDetails
                .Include(sd => sd.Product) // We need the product's name
                                           // Group all sale details by the ProductId
                .GroupBy(sd => new { sd.ProductId, sd.Product.Name })
                // From each group, create a new TopSellingProductDto
                .Select(group => new TopSellingProductDto
                {
                    ProductId = group.Key.ProductId,
                    ProductName = group.Key.Name,
                    // Sum the quantity of all items in the group to get total units sold
                    TotalSold = group.Sum(sd => sd.Quantity),
                    // Sum the (quantity * unit price) for all items to get total revenue
                    TotalRevenue = group.Sum(sd => sd.Quantity * sd.UnitPrice)
                })
                // Order the results by the highest revenue first
                .OrderByDescending(p => p.TotalRevenue)
                // Take only the top 'count' products (defaults to 5)
                .Take(count)
                .ToListAsync();

            return Ok(topProducts);
        }

        // GET: api/dashboard/salesovertime?period=week
        [HttpGet("salesovertime")]
        public async Task<ActionResult<IEnumerable<SalesDataPointDto>>> GetSalesOverTime([FromQuery] string period = "week")
        {
            List<SalesDataPointDto> salesData;
            var today = DateTime.UtcNow.Date;

            switch (period.ToLower())
            {
                case "year":
                    // Get the raw data from the DB first
                    var yearlyData = await _context.Sales
                        .Where(s => s.SaleDate >= today.AddMonths(-12))
                        .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                        .Select(group => new
                        {
                            Year = group.Key.Year,
                            Month = group.Key.Month,
                            TotalSales = group.Sum(s => s.FinalAmount)
                        })
                        .OrderBy(s => s.Year).ThenBy(s => s.Month)
                        .ToListAsync();

                    // Now, format the data in C#
                    salesData = yearlyData.Select(s => new SalesDataPointDto
                    {
                        Label = new DateTime(s.Year, s.Month, 1).ToString("MMM yyyy"),
                        TotalSales = s.TotalSales
                    }).ToList();
                    break;

                case "month":
                    // Get the raw data
                    var monthlyData = await _context.Sales
                        .Where(s => s.SaleDate >= today.AddDays(-30))
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(group => new
                        {
                            Date = group.Key,
                            TotalSales = group.Sum(s => s.FinalAmount)
                        })
                        .OrderBy(s => s.Date)
                        .ToListAsync();

                    // Format in C#
                    salesData = monthlyData.Select(s => new SalesDataPointDto
                    {
                        Label = s.Date.ToString("yyyy-MM-dd"),
                        TotalSales = s.TotalSales
                    }).ToList();
                    break;

                case "week":
                default:
                    // Get the raw data from the DB
                    var weeklyData = await _context.Sales
                        .Where(s => s.SaleDate >= today.AddDays(-7))
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(group => new
                        {
                            Date = group.Key,
                            TotalSales = group.Sum(s => s.FinalAmount)
                        })
                        .OrderBy(s => s.Date) // Order by date first
                        .ToListAsync();

                    // Now, format the data in C#
                    salesData = weeklyData.Select(s => new SalesDataPointDto
                    {
                        Label = s.Date.DayOfWeek.ToString(), // This C# method now works
                        TotalSales = s.TotalSales
                    }).ToList();
                    break;
            }

            return Ok(salesData);
        }

        // GET: api/dashboard/cashierperformance
        [HttpGet("cashierperformance")]
        public async Task<ActionResult<IEnumerable<CashierPerformanceDto>>> GetCashierPerformance([FromQuery] string period = "today")
        {
            // Define the date range based on the period
            var startDate = DateTime.UtcNow.Date;
            if (period.ToLower() == "week")
            {
                startDate = DateTime.UtcNow.Date.AddDays(-7);
            }
            else if (period.ToLower() == "month")
            {
                startDate = DateTime.UtcNow.Date.AddDays(-30);
            }

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
        [HttpGet("topclients")]
        public async Task<ActionResult<IEnumerable<TopClientDto>>> GetTopClients([FromQuery] int count = 5)
        {
            var topClients = await _context.Customers
                // We are querying the Customers table directly
                .Select(c => new TopClientDto
                {
                    CustomerId = c.Id,
                    CustomerName = c.Name,
                    // For each customer, calculate the sum of FinalAmount from their related sales
                    TotalSpent = c.Sales.Sum(s => s.FinalAmount),
                    // Count the number of sales they have made
                    TotalOrders = c.Sales.Count(),
                    // Get their current outstanding balance (we only care if they owe money)
                    OutstandingBalance = c.CurrentBalance < 0 ? c.CurrentBalance * -1 : 0
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(count)
                .ToListAsync();

            return Ok(topClients);
        }

        private decimal CalculatePercentageChange(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return Math.Round(((current - previous) / previous) * 100, 1);
        }
    }
}