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

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}