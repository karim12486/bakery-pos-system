using System.ComponentModel.DataAnnotations.Schema;
using BakeryPOS.API.Core.Enums;

namespace BakeryPOS.API.Core.Entities
{
    public class Sale
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        public PaymentType PaymentMethod { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountPaid { get; set; } // The initial deposit or full amount

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountOwed { get; set; } // TotalAmount - AmountPaid

        // Foreign Key to Customer (optional)
        public int? CustomerId { get; set; } // Nullable, as not every sale is for a premium customer
        public Customer? Customer { get; set; }

        // Foreign Key to the User who made the sale
        public int UserId { get; set; }
        public User User { get; set; }

        // Navigation property for all the items in this sale
        public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();
    }
}