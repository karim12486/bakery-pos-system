using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class CustomerPaymentDto
    {
        [Required]
        [Range(0.01, 999999)]
        public decimal AmountPaid { get; set; }
        public string? Notes { get; set; }
    }
}
