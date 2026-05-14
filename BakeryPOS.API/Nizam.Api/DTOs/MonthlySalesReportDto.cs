namespace Nizam.Api.DTOs
{
    // We can reuse existing DTOs for the lists
    // using TopSellingProductDto;
    // using CashierPerformanceDto;

    public class TopCustomerDto // A simplified version for this report
    {
        public string CustomerName { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class DailyBreakdownDto // A simple DTO for the daily breakdown
    {
        public int Day { get; set; }
        public decimal TotalSales { get; set; }
    }

    public class MonthlySalesReportDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int GrandTotalTransactions { get; set; }
        public decimal GrandTotalSalesValue { get; set; }
        public decimal GrandTotalDiscountAmount { get; set; }
        public decimal AverageTransactionValue => GrandTotalTransactions > 0 ? GrandTotalSalesValue / GrandTotalTransactions : 0;

        // --- NEW DETAILED PROPERTIES ---
        public List<TopSellingProductDto> TopSellingProducts { get; set; } = new List<TopSellingProductDto>();
        public List<CashierPerformanceDto> CashierPerformance { get; set; } = new List<CashierPerformanceDto>();
        public List<TopCustomerDto> TopCustomers { get; set; } = new List<TopCustomerDto>();
        public List<DailyBreakdownDto> DailySalesBreakdown { get; set; } = new List<DailyBreakdownDto>();
        public List<PaymentMethodStatsDto> PaymentBreakdown { get; set; } = new();
    }
}