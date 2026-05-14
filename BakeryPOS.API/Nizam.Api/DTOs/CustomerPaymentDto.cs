using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs
{
    public class CustomerPaymentDto
    {
        [Required]
        [Range(0.01, 999999)]
        public decimal AmountPaid { get; set; }
        public string? Notes { get; set; }
    }
}
