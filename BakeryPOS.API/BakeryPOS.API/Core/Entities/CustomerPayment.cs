using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities
{
    public class CustomerPayment
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountPaid { get; set; }

        public string? Notes { get; set; }

        // Foreign Key to the Customer who made the payment
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        // Foreign Key to the User who recorded the payment
        public int UserId { get; set; }
        public User User { get; set; }
    }
}