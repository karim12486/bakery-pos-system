namespace Nizam.Api.Core.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Routes this category's products to a kitchen station (Phase B / KDS).
        /// Null = not routed (counter items, retail goods) — those never appear on a KDS screen.</summary>
        public int? KitchenStationId { get; set; }
        public KitchenStation? KitchenStation { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}