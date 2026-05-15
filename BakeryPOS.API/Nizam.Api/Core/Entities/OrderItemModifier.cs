namespace Nizam.Api.Core.Entities;

/// <summary>
/// A single modifier choice applied to an <see cref="OrderItem"/>. Stored as a child row
/// (replacing the legacy <see cref="OrderItem.Modifiers"/> JSON column) so reporting can
/// aggregate by modifier and the AI Insights add-on can see structured data.
///
/// <para><b>Snapshotting</b>: <see cref="GroupName"/>, <see cref="Name"/>, and
/// <see cref="PriceDelta"/> are captured AT SALE TIME. Editing the source <see cref="Modifier"/>
/// later does not touch historical rows — receipts and reports stay historically accurate.</para>
///
/// <para><b>Source links</b>: <see cref="ModifierId"/> and <see cref="ModifierGroupId"/>
/// are nullable so analytics can still group historical orders even after the source modifier
/// is soft-deleted (or hard-deleted in a future cleanup). The snapshot fields are the source
/// of truth for display; the IDs are for joins.</para>
/// </summary>
public class OrderItemModifier
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    /// <summary>Source modifier id (nullable — keeps the row valid if the modifier is deleted later).</summary>
    public int? ModifierId { get; set; }

    /// <summary>Source group id (nullable for the same reason).</summary>
    public int? ModifierGroupId { get; set; }

    /// <summary>Snapshotted group label at sale time — used for receipt grouping ("Size: Large").</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>Snapshotted modifier name at sale time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Snapshotted price delta at sale time. Per-unit; multiplied by the order-item
    /// quantity in line-total calculations.</summary>
    public decimal PriceDelta { get; set; }
}
