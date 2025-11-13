namespace BakeryPOS.API.DTOs
{
    public class CashierPerformanceDto
    {
        public int UserId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalSalesValue { get; set; }
    }
}