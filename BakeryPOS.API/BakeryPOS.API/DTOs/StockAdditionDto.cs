using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class StockAdditionDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, 10000, ErrorMessage = "Quantity to add must be at least 1.")]
        public int QuantityToAdd { get; set; }
    }
}