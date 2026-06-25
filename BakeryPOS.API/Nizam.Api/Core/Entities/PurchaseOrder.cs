using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A purchase order to a supplier (Phase 3.7 / inventory_ops). Draft → Submitted → Received
/// (or Cancelled). Receiving the order increments product stock from each line's received
/// quantity and writes a <c>Purchase</c> stock movement per line. Tenant-scoped.
/// </summary>
public class PurchaseOrder
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Branch the stock is received into. Null = tenant-wide (single-branch tenants).</summary>
    public int? BranchId { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    /// <summary>Optional human reference / supplier invoice number.</summary>
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PurchaseOrderItem> Items { get; set; } = new();
}

/// <summary>A single product line on a <see cref="PurchaseOrder"/>.</summary>
public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int QuantityOrdered { get; set; }

    /// <summary>Quantity actually received (set at receive time; defaults to ordered).</summary>
    public int QuantityReceived { get; set; }

    /// <summary>Unit cost paid; used to update product cost + roll up the PO total.</summary>
    public decimal UnitCost { get; set; }
}
