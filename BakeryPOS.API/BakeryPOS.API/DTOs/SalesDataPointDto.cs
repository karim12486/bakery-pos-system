namespace BakeryPOS.API.DTOs
{
    public class SalesDataPointDto
    {
        public string Label { get; set; } // e.g., "Monday", "2025-11-05"
        public decimal TotalSales { get; set; }
    }
}