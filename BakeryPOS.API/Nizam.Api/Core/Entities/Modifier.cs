namespace Nizam.Api.Core.Entities;

/// <summary>
/// A single option within a <see cref="ModifierGroup"/>. Carries a price delta that's
/// added to the base product price when selected. Deltas can be zero (size variants
/// priced identically) or negative (e.g. -5 EGP for "skip the cheese" discount).
///
/// <para><b>Price snapshotting</b>: when applied to an order item, the delta is
/// snapshotted at order time (in <c>OrderItemModifier</c>, future branch). Historical
/// orders never recalculate, so editing a modifier's price doesn't reach back into
/// closed sales.</para>
/// </summary>
public class Modifier
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ModifierGroupId { get; set; }

    /// <summary>Label shown in the picker (e.g. "Large", "Oat milk").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Amount added to the base product price when this modifier is selected.
    /// Can be 0 (free variant) or negative (discount).</summary>
    public decimal PriceDelta { get; set; }

    /// <summary>Lower sort_order = displayed earlier within the group.</summary>
    public int SortOrder { get; set; }

    /// <summary>If true, this modifier is pre-selected when the picker opens.
    /// Only meaningful for single-choice groups.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ModifierGroup? Group { get; set; }
}
