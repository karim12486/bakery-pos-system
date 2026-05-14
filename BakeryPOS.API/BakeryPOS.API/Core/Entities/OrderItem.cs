using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities;

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

    /// <summary>JSON array of modifier choices (e.g. <c>[{"id":12,"name":"No Ice","priceDelta":0}]</c>).
    /// Always empty in Phase A; populated in Phase B by the modifier picker.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string Modifiers { get; set; } = "[]";

    /// <summary>Kitchen-side state. Phase A items go straight to <c>Closed</c>.</summary>
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Closed;

    public DateTime? FiredAt { get; set; }
    public DateTime? ServedAt { get; set; }

    /// <summary>Optional free-form notes (e.g. "extra hot", "no nuts").</summary>
    public string? Notes { get; set; }
}

public enum OrderItemStatus
{
    /// <summary>Just added to the order. Phase B.</summary>
    Pending,

    /// <summary>Sent to kitchen. Phase B.</summary>
    Fired,

    /// <summary>Prepared and handed to customer. Phase B.</summary>
    Served,

    /// <summary>Closed (Phase A: immediate). Voided lines also end here.</summary>
    Closed
}
