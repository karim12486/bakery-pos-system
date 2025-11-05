using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services
{
    public class ReportGenerationService : IReportGenerationService
    {
        private readonly AppDbContext _context;

        public ReportGenerationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DailySalesReportDto> GenerateDailySalesReportAsync(DateTime date)
        {
            // Define the start and end of the day for the given date
            var startDate = date.Date; // e.g., 2025-11-05 00:00:00
            var endDate = startDate.AddDays(1);   // e.g., 2025-11-06 00:00:00

            // Query the sales within that date range
            var salesForDay = await _context.Sales
                .Include(s => s.User) // Include the User to get the cashier's name
                .Where(s => s.SaleDate >= startDate && s.SaleDate < endDate)
                .ToListAsync();

            // Use LINQ to group the sales by the cashier who made them
            var salesByCashier = salesForDay
                .GroupBy(s => s.User)
                .Select(group => new CashierSalesReportDto
                {
                    CashierName = group.Key.FullName,
                    TotalTransactions = group.Count(), // Count the number of sales in the group
                    TotalSalesValue = group.Sum(s => s.TotalAmount) // Sum the total amount for each sale
                })
                .ToList();

            // Assemble the final report object
            var report = new DailySalesReportDto
            {
                ReportDate = startDate,
                SalesByCashier = salesByCashier,
                GrandTotalTransactions = salesForDay.Count,
                GrandTotalSalesValue = salesForDay.Sum(s => s.TotalAmount)
            };

            return report;
        }
    }
}