namespace BakeryPOS.API.Core.Entities
{
    public enum StockMovementType
    {
        Addition,      // Manually adding new stock
        Sale,          // Stock removed due to a sale
        Return,        // Stock added due to a return
        Adjustment     // Manual correction (e.g., due to spoilage or counting error)
    }

    public class StockMovement
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public int QuantityChange { get; set; } // Can be positive (addition) or negative (removal)

        public StockMovementType Type { get; set; }

        // Foreign Key to the Product being adjusted
        public int ProductId { get; set; }
        public Product Product { get; set; }

        // Foreign Key to the User who made the change
        public int UserId { get; set; }
        public User User { get; set; }
    }
}