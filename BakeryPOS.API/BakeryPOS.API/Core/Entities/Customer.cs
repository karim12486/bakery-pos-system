using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal CurrentBalance { get; set; } = 0; // Positive means customer has credit, negative means they owe money

        [Column(TypeName = "decimal(5, 2)")]
        public decimal DiscountPercentage { get; set; } = 0; // Represents the discount percentage for this specific customer (e.g., 15.00 for 15%)
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}