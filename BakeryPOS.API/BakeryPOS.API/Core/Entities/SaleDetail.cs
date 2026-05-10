using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities
{
    public class SaleDetail
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; } // The price of the product at the time of sale

        // Foreign Key to the Sale this detail belongs to
        public int SaleId { get; set; }
        public Sale Sale { get; set; }

        // Foreign Key to the Product being sold
        public int ProductId { get; set; }
        public Product Product { get; set; }
    }
}