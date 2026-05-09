using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class ProductForUpdateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Barcode { get; set; }

        [Required]
        [Range(0, 10000)]
        public decimal CostPrice { get; set; }

        public bool IsSpecial { get; set; }

        [Required]
        [Range(0.01, 10000, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, 100000, ErrorMessage = "Stock must be a non-negative number.")]
        public decimal StockQuantity { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; }
        [Required]
        public int CategoryId { get; set; }
    }
}