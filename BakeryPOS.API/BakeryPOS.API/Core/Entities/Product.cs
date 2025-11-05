using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Barcode { get; set; } // Barcode can be optional

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal CostPrice { get; set; } // The price you paid to acquire the product

        public bool IsSpecial { get; set; } = false; // Flag to include this in the special report

        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; } // Optional image for the POS display
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}