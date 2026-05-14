using System.ComponentModel.DataAnnotations.Schema;
using BakeryPOS.API.Core.Enums;

namespace BakeryPOS.API.Core.Entities;

/// <summary>
/// The customer's intent — what was ordered. <see cref="Sale"/> records the PAYMENT that
/// closed this order; the two are 1:1 in Phase A (counter-service: paid at the same moment
/// items are ordered) and may become 1:N in Phase B (split bills, partial payments over time).
///
/// Phase A always creates orders with <c>Status = Closed</c>, <c>Channel = Takeaway</c>,
/// <c>TableId = null</c>, and empty <c>OrderItem.Modifiers</c>. The B-aware columns exist
/// from day 1 so Phase B can populate them without a schema migration.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BranchId { get; set; }

    /// <summary>Cashier who opened the order.</summary>
    public int CashierUserId { get; set; }
    public User? Cashier { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>FK to <c>Shift</c> — null until the Shift feature ships.</summary>
    public int? ShiftId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Closed;
    public OrderChannel Channel { get; set; } = OrderChannel.Takeaway;

    /// <summary>Reserved for Phase B (table-service). Always null in Phase A.</summary>
    public int? TableId { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When kitchen received the ticket. Phase B.</summary>
    public DateTime? FiredAt { get; set; }

    /// <summary>When all items were served / handed to customer. Phase B.</summary>
    public DateTime? ServedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal FinalAmount { get; set; }

    /// <summary>Optional free-form notes (special instructions, customer preferences).</summary>
    public string? Notes { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
