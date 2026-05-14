namespace Nizam.Api.DTOs
{
    public class CashierPerformanceDto
    {
        public int UserId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalSalesValue { get; set; }
        public decimal AverageSaleValue { get; set; }
    }
}