using System.ComponentModel.DataAnnotations.Schema;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A single line on an <see cref="Order"/>. In Phase A this is just product + quantity +
/// price snapshot. In Phase B the same row carries kitchen-side state (Fired/Served timestamps,
/// modifier choices, special instructions).
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Decimal to accommodate by-weight items (bread by gram, deli by kg).</summary>
    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    /// <summary>Price-per-unit captured at the moment of sale. Doesn't update if the product
    /// price changes later — receipts and reports stay historically accurate.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity × UnitPrice, optionally minus per-line discounts. Stored to avoid
    /// recomputation drift on reports.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LineTotal { get; set; }

    /// <summary>Per-item tax amount (if branch tax rate applies). Phase A pro-rates the
    /// order-level tax across items; Phase B may allow per-modifier tax rates.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }

    /// <summary>Legacy JSON column — superseded by <see cref="AppliedModifiers"/>. New code writes
    /// to the child table; this column stays at <c>"[]"</c> on new orders. Kept as a column for
    /// schema compatibility with historical rows; a future cleanup branch may drop it.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string Modifiers { get; set; } = "[]";

    /// <summary>Snapshotted modifier choices for this item. Source of truth for receipt rendering
    /// and reporting. See <see cref="OrderItemModifier"/> for the snapshot contract.</summary>
    public ICollection<OrderItemModifier> AppliedModifiers { get; set; } = new List<OrderItemModifier>();

    /// <summary>Kitchen station this item is routed to, snapshotted at order time from the
    /// product's category. Null = unrouted (counter/retail) — won't surface on any KDS screen.
    /// Denormalised so changing a category's station later doesn't re-route historical items.</summary>
    public int? KitchenStationId { get; set; }

    /// <summary>Coursing: which course this item belongs to (1 = first/appetizers, 2 = mains,
    /// 3 = dessert, …). Defaults to 1. Servers fire a whole course at once so the kitchen
    /// paces the meal. Phase B / dine-in.</summary>
    public int CourseNumber { get; set; } = 1;

    /// <summary>Kitchen-side state. Phase A items go straight to <c>Closed</c>.</summary>
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Closed;

    public DateTime? FiredAt { get; set; }
    public DateTime? ServedAt { get; set; }

    /// <summary>Optional free-form notes (e.g. "extra hot", "no nuts").</summary>
    public string? Notes { get; set; }
}

public enum OrderItemStatus
{
    // NOTE: values are appended (not reordered) to keep int storage stable for existing rows.

    /// <summary>Just added to the order, not yet sent to the kitchen. Phase B.</summary>
    Pending,

    /// <summary>Sent to kitchen — appears on the KDS. Phase B.</summary>
    Fired,

    /// <summary>Prepared and handed to customer / served. Phase B.</summary>
    Served,

    /// <summary>Closed (Phase A: immediate on sale).</summary>
    Closed,

    /// <summary>Kitchen has started preparing (optional intermediate; some configs skip
    /// straight from Fired to Ready). Phase B / KDS.</summary>
    Cooking,

    /// <summary>Prepared and waiting to be picked up / run to the table. Phase B / KDS.</summary>
    Ready,

    /// <summary>Cancelled before completion (with reason + audit). Phase B / KDS.</summary>
    Voided
}
