using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class SaleDetailForCreateDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(0.001, 10000, ErrorMessage = "Quantity must be greater than 1 gram")]
        public decimal Quantity { get; set; }
    }
}