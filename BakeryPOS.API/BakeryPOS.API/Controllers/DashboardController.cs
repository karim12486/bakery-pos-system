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
        [HttpGet("salesovertime")]
        public async Task<ActionResult<IEnumerable<SalesDataPointDto>>> GetSalesOverTime([FromQuery] string period = "week")
        {
            List<SalesDataPointDto> finalResult = new List<SalesDataPointDto>();
            var today = DateTime.UtcNow.Date;

            // We use InvariantCulture or En-US to ensure "Jan", "Feb", "Sun", "Mon" format 
            // regardless of server settings, as requested.
            var culture = new System.Globalization.CultureInfo("en-US");

            if (period.ToLower() == "year")
            {
                // --- YEAR VIEW: Current Year (Jan - Dec) ---
                var startOfYear = new DateTime(today.Year, 1, 1);
                var endOfYear = startOfYear.AddYears(1);

                // 1. Get raw data for the current year
                var dbData = await _context.Sales
                    .Where(s => s.SaleDate >= startOfYear && s.SaleDate < endOfYear)
                    .GroupBy(s => s.SaleDate.Month)
                    .Select(g => new { Month = g.Key, Total = g.Sum(s => s.FinalAmount) })
                    .ToListAsync();

                // 2. Loop 1-12 to ensure every month is represented
                for (int m = 1; m <= 12; m++)
                {
                    var monthName = new DateTime(today.Year, m, 1).ToString("MMM", culture); // "Jan", "Feb"
                    var salesForMonth = dbData.FirstOrDefault(d => d.Month == m)?.Total ?? 0;

                    finalResult.Add(new SalesDataPointDto
                    {
                        Label = monthName,
                        TotalSales = salesForMonth
                    });
                }
            }
            else if (period.ToLower() == "month")
            {
                // --- MONTH VIEW: Current Month (Week 1 - Week 5) ---
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1);

                // 1. Get all sales for this month
                var dbData = await _context.Sales
                    .Where(s => s.SaleDate >= startOfMonth && s.SaleDate < endOfMonth)
                    .Select(s => new { s.SaleDate.Day, s.FinalAmount })
                    .ToListAsync();

                // 2. Create "Weeks" logic manually
                // Week 1: Days 1-7, Week 2: 8-14, etc.
                int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

                // We assume a max of 5 weeks cover any month
                for (int w = 0; w < 5; w++)
                {
                    int startDay = (w * 7) + 1;
                    int endDay = startDay + 6;

                    // Stop if the start of this "week" is beyond the end of the month
                    if (startDay > daysInMonth) break;

                    // Cap the end day to the last day of the month
                    if (endDay > daysInMonth) endDay = daysInMonth;

                    // Sum sales that fall within this day range
                    var weeklySales = dbData
                        .Where(d => d.Day >= startDay && d.Day <= endDay)
                        .Sum(d => d.FinalAmount);

                    finalResult.Add(new SalesDataPointDto
                    {
                        Label = $"Week {w + 1}", // "Week 1", "Week 2"
                        TotalSales = weeklySales
                    });
                }
            }
            else
            {
                // --- WEEK VIEW (Default): Past 7 Days (e.g., Tue -> Mon) ---
                // Start from 6 days ago up to today (7 days total)

                // 1. Get raw data for the date range
                var startOfRange = today.AddDays(-6);
                var endOfRange = today.AddDays(1); // Up to tomorrow midnight

                var dbData = await _context.Sales
                    .Where(s => s.SaleDate >= startOfRange && s.SaleDate < endOfRange)
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new { Date = g.Key, Total = g.Sum(s => s.FinalAmount) })
                    .ToListAsync();

                // 2. Loop from 6 days ago to Today
                for (int i = 6; i >= 0; i--)
                {
                    var targetDate = today.AddDays(-i);
                    var dayName = targetDate.ToString("ddd", culture); // "Sun", "Mon"

                    var salesForDay = dbData.FirstOrDefault(d => d.Date == targetDate)?.Total ?? 0;

                    finalResult.Add(new SalesDataPointDto
                    {
                        Label = dayName,
                        TotalSales = salesForDay
                    });
                }
            }

            return Ok(finalResult);
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

        // GET: api/dashboard/financialstats?period=month
        [HttpGet("financialstats")]
        public async Task<ActionResult<IEnumerable<FinancialStatsDto>>> GetFinancialStats([FromQuery] string period = "month")
        {
            var today = DateTime.UtcNow.Date;
            List<FinancialStatsDto> stats = new List<FinancialStatsDto>();

            if (period.ToLower() == "year")
            {
                // --- YEAR VIEW: Group by Month (Jan, Feb...) ---
                var startOfYear = new DateTime(today.Year, 1, 1);

                // 1. Get Sales
                var salesData = await _context.Sales
                    .Where(s => s.SaleDate >= startOfYear)
                    .GroupBy(s => s.SaleDate.Month)
                    .Select(g => new { Month = g.Key, Amount = g.Sum(s => s.FinalAmount) })
                    .ToListAsync();

                // 2. Get Expenses
                var expenseData = await _context.Expenses
                    .Where(e => e.Date >= startOfYear)
                    .GroupBy(e => e.Date.Month)
                    .Select(g => new { Month = g.Key, Amount = g.Sum(e => e.Amount) })
                    .ToListAsync();

                // 3. Merge (12 Months)
                for (int i = 1; i <= 12; i++)
                {
                    var revenue = salesData.FirstOrDefault(s => s.Month == i)?.Amount ?? 0;
                    var expense = expenseData.FirstOrDefault(e => e.Month == i)?.Amount ?? 0;

                    stats.Add(new FinancialStatsDto
                    {
                        Label = new DateTime(today.Year, i, 1).ToString("MMM"), // "Jan", "Feb"
                        Revenue = revenue,
                        Expenses = expense
                    });
                }
            }
            else if (period.ToLower() == "month")
            {
                // --- MONTH VIEW: Group by Week (Week 1, Week 2...) ---
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1);

                // 1. Get raw data for the month
                var salesData = await _context.Sales
                    .Where(s => s.SaleDate >= startOfMonth && s.SaleDate < endOfMonth)
                    .Select(s => new { s.SaleDate, s.FinalAmount })
                    .ToListAsync();

                var expenseData = await _context.Expenses
                    .Where(e => e.Date >= startOfMonth && e.Date < endOfMonth)
                    .Select(e => new { e.Date, e.Amount })
                    .ToListAsync();

                // 2. Group into 4-5 Weeks in Memory
                // Logic: Week 1 = Days 1-7, Week 2 = Days 8-14, etc.
                var weeksInMonth = Enumerable.Range(0, 5); // Approx 5 weeks max

                foreach (var w in weeksInMonth)
                {
                    var weekStartDay = (w * 7) + 1;
                    var weekEndDay = (w * 7) + 7;

                    // Filter data for this specific week range
                    var weekRevenue = salesData
                        .Where(s => s.SaleDate.Day >= weekStartDay && s.SaleDate.Day <= weekEndDay)
                        .Sum(s => s.FinalAmount);

                    var weekExpense = expenseData
                        .Where(e => e.Date.Day >= weekStartDay && e.Date.Day <= weekEndDay)
                        .Sum(e => e.Amount);

                    // Only add if the week actually exists in this month (e.g. ignore Week 5 if month has 28 days)
                    if (weekStartDay <= DateTime.DaysInMonth(today.Year, today.Month))
                    {
                        stats.Add(new FinancialStatsDto
                        {
                            Label = $"Week {w + 1}",
                            Revenue = weekRevenue,
                            Expenses = weekExpense
                        });
                    }
                }
            }
            else
            {
                // --- WEEK VIEW: Group by Day (Mon, Tue...) ---
                // Default to last 7 days
                var startOfWeek = today.AddDays(-6); // Go back 6 days + today = 7 days

                // 1. Get Sales
                var salesData = await _context.Sales
                    .Where(s => s.SaleDate >= startOfWeek)
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new { Date = g.Key, Amount = g.Sum(s => s.FinalAmount) })
                    .ToListAsync();

                // 2. Get Expenses
                var expenseData = await _context.Expenses
                    .Where(e => e.Date >= startOfWeek)
                    .GroupBy(e => e.Date.Date)
                    .Select(g => new { Date = g.Key, Amount = g.Sum(e => e.Amount) })
                    .ToListAsync();

                // 3. Merge (7 Days)
                for (int i = 0; i < 7; i++)
                {
                    var date = startOfWeek.AddDays(i);
                    var revenue = salesData.FirstOrDefault(s => s.Date == date)?.Amount ?? 0;
                    var expense = expenseData.FirstOrDefault(e => e.Date == date)?.Amount ?? 0;

                    stats.Add(new FinancialStatsDto
                    {
                        Label = date.DayOfWeek.ToString(), // "Monday", "Tuesday"
                        Revenue = revenue,
                        Expenses = expense
                    });
                }
            }

            return Ok(stats);
        }
    }
}