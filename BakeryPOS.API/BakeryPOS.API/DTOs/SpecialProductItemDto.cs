namespace BakeryPOS.API.DTOs
{
    public class SpecialProductItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal QuantityAdded { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Profit { get; set; }
        // --- ADD THIS ---
        public decimal ProfitMargin => TotalRevenue > 0 ? (Profit / TotalRevenue) * 100 : 0;
    }
}