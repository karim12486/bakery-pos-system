namespace Nizam.Api.Core.Entities
{
    public enum StockMovementType
    {
        Addition,      // Manually adding new stock
        Sale,          // Stock removed due to a sale
        Return,        // Stock added due to a return
        Adjustment,    // Manual correction (e.g., due to spoilage or counting error)
        Purchase,      // Stock received against a purchase order (Phase 3.7)
        Waste,         // Stock written off via the waste log (Phase 3.7)
        TransferOut,   // Stock dispatched to another branch (Phase 3.7)
        TransferIn     // Stock received from another branch (Phase 3.7)
    }

    public class StockMovement
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
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