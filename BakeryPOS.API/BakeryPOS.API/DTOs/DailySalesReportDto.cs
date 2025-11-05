namespace BakeryPOS.API.DTOs
{
    public class DailySalesReportDto
    {
        public DateTime ReportDate { get; set; }
        public int GrandTotalTransactions { get; set; }
        public decimal GrandTotalSalesValue { get; set; }
        public List<CashierSalesReportDto> SalesByCashier { get; set; } = new List<CashierSalesReportDto>();
    }
}