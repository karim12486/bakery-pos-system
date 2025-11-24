namespace BakeryPOS.API.DTOs
{
    public class ExpenseSummaryDto
    {
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public string Period { get; set; } = string.Empty; // e.g., "November 2025"
    }
}