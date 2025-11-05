namespace BakeryPOS.API.DTOs
{
    public class CashierSalesReportDto
    {
        public string CashierName { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalSalesValue { get; set; }
    }
}