namespace BakeryPOS.API.DTOs
{
    public class TopSellingProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal GrowthPercentage { get; set; }
    }
}