namespace BakeryPOS.API.DTOs
{
    public class StockMovementDto
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal QuantityChange { get; set; }
        public string Type { get; set; } = string.Empty; // e.g., "Addition", "Sale"
        public string ProductName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}