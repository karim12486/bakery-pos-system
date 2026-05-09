namespace BakeryPOS.API.DTOs
{
    public class SaleItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => Quantity * UnitPrice; // Calculated property
    }
}