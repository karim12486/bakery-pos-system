namespace BakeryPOS.API.Core.Entities
{
    public enum RequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class RemovalRequest
    {
        public int Id { get; set; }
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        // What is being requested for removal?
        // For now, let's keep it simple: a product name and price.
        // In a real system, you might link this to a temporary "cart item ID".
        public string ProductName { get; set; }
        public decimal ProductPrice { get; set; }

        // Who requested it?
        public int RequestingUserId { get; set; }
        public User RequestingUser { get; set; }

        // Who responded to it? (nullable)
        public int? ApprovingUserId { get; set; }
        public User? ApprovingUser { get; set; }

        public RequestStatus Status { get; set; } = RequestStatus.Pending;
    }
}