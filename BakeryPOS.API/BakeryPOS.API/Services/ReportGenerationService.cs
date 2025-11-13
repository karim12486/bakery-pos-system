using BakeryPOS.API.Core.Entities;
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
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            var salesForDay = await _context.Sales
                .Include(s => s.User)
                .Where(s => s.SaleDate >= startDate && s.SaleDate < endDate)
                .ToListAsync();

            // --- Recalculate totals for robustness ---
            var grandTotalSalesValue = salesForDay.Sum(s => s.TotalAmount - s.DiscountAmount);

            var salesByCashier = salesForDay
                .GroupBy(s => s.User)
                .Select(group => new CashierSalesReportDto
                {
                    CashierName = group.Key.FullName,
                    TotalTransactions = group.Count(),
                    TotalSalesValue = group.Sum(s => s.TotalAmount - s.DiscountAmount) // Use recalculated value here too
                })
                .ToList();

            var report = new DailySalesReportDto
            {
                ReportDate = startDate,
                SalesByCashier = salesByCashier,
                GrandTotalTransactions = salesForDay.Count,
                GrandTotalSalesValue = grandTotalSalesValue // Use recalculated value
            };

            return report;
        }

        public async Task<MonthlySalesReportDto> GenerateMonthlySalesReportAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var salesForMonth = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.Customer)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.Product)
                .Where(s => s.SaleDate >= startDate && s.SaleDate < endDate)
                .ToListAsync();

            // --- RECALCULATE TOTALS HERE TO BE SAFE ---
            var grandTotalSalesValue = salesForMonth.Sum(s => s.TotalAmount - s.DiscountAmount);
            var grandTotalDiscountAmount = salesForMonth.Sum(s => s.DiscountAmount);

            // 1. Top Selling Products
            var topProducts = salesForMonth
                .SelectMany(s => s.SaleDetails)
                .GroupBy(sd => sd.Product)
                .Select(group => new TopSellingProductDto
                {
                    ProductId = group.Key.Id,
                    ProductName = group.Key.Name,
                    TotalSold = group.Sum(sd => sd.Quantity),
                    TotalRevenue = group.Sum(sd => sd.Quantity * sd.UnitPrice)
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(5)
                .ToList();

            // 2. Cashier Performance
            var cashierPerformance = salesForMonth
                .GroupBy(s => s.User)
                .Select(group => new CashierPerformanceDto
                {
                    UserId = group.Key.Id,
                    CashierName = group.Key.FullName,
                    TotalTransactions = group.Count(),
                    TotalSalesValue = group.Sum(s => s.TotalAmount - s.DiscountAmount) // Recalculate
                })
                .OrderByDescending(p => p.TotalSalesValue)
                .ToList();

            // 3. Top Customers
            var topCustomers = salesForMonth
                .Where(s => s.Customer != null)
                .GroupBy(s => s.Customer)
                .Select(group => new TopCustomerDto
                {
                    CustomerName = group.Key.Name,
                    TotalSpent = group.Sum(s => s.TotalAmount - s.DiscountAmount) // Recalculate
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(5)
                .ToList();

            // 4. Daily Sales Breakdown
            var dailySales = salesForMonth
                .GroupBy(s => s.SaleDate.Day)
                .Select(group => new DailyBreakdownDto
                {
                    Day = group.Key,
                    TotalSales = group.Sum(s => s.TotalAmount - s.DiscountAmount) // Recalculate
                })
                .OrderBy(d => d.Day)
                .ToList();

            // Assemble the final report object with our recalculated totals
            var report = new MonthlySalesReportDto
            {
                Year = year,
                Month = month,
                GrandTotalTransactions = salesForMonth.Count,
                GrandTotalSalesValue = grandTotalSalesValue, // Use recalculated value
                GrandTotalDiscountAmount = grandTotalDiscountAmount,
                TopSellingProducts = topProducts,
                CashierPerformance = cashierPerformance,
                TopCustomers = topCustomers,
                DailySalesBreakdown = dailySales
            };

            return report;
        }

        public async Task<SpecialProductReportDto> GenerateSpecialProductReportAsync(DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            var specialProducts = await _context.Products
                .Where(p => p.IsSpecial && p.IsActive)
                .ToListAsync();

            var report = new SpecialProductReportDto { ReportDate = startDate };

            foreach (var product in specialProducts)
            {
                // 1. Calculate Quantity Added
                var quantityAdded = await _context.StockMovements
                    .Where(sm => sm.ProductId == product.Id &&
                                   sm.Type == StockMovementType.Addition &&
                                   sm.Timestamp >= startDate && sm.Timestamp < endDate)
                    .SumAsync(sm => sm.QuantityChange);

                // 2. Calculate Sales Data
                var salesDetails = await _context.SaleDetails
                    .Where(sd => sd.ProductId == product.Id &&
                                   sd.Sale.SaleDate >= startDate && sd.Sale.SaleDate < endDate)
                    .ToListAsync();

                var quantitySold = salesDetails.Sum(sd => sd.Quantity);
                var totalRevenue = salesDetails.Sum(sd => sd.Quantity * sd.UnitPrice);

                // 3. Calculate Profit
                var totalCost = quantitySold * product.CostPrice;
                var profit = totalRevenue - totalCost;

                report.ProductDetails.Add(new SpecialProductItemDto
                {
                    ProductName = product.Name,
                    QuantityAdded = quantityAdded,
                    QuantitySold = quantitySold,
                    TotalRevenue = totalRevenue,
                    TotalCost = totalCost,
                    Profit = profit
                });
            }
            return report;
        }
    }
}