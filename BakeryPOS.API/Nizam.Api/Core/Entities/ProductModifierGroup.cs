namespace Nizam.Api.Core.Entities;

/// <summary>
/// Many-to-many join row: which modifier groups apply to which products. One product can
/// have many groups attached (a Latte has Size + Milk + Extras); one group can apply to
/// many products (the "Extras" group is shared across all hot drinks).
///
/// <para>Unique (TenantId, ProductId, ModifierGroupId) prevents duplicate attachments.
/// <see cref="SortOrder"/> controls the order groups appear within a product's picker.</para>
/// </summary>
public class ProductModifierGroup
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public int ModifierGroupId { get; set; }

    /// <summary>Lower sort_order = group appears earlier in the picker for this product.</summary>
    public int SortOrder { get; set; }

    public Product? Product { get; set; }
    public ModifierGroup? ModifierGroup { get; set; }
}
