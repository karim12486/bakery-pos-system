using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class StockAdditionDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(0.001, 100000, ErrorMessage = "Quantity must be greater than 1 gram")]
        public decimal QuantityToAdd { get; set; }
    }
}