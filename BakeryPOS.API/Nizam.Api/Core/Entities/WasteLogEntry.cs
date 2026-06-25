using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A stock write-off (Phase 3.7 / inventory_ops). Recording an entry decrements
/// <c>Product.StockQuantity</c> and writes a <c>Waste</c> stock movement. Immutable once
/// recorded. Tenant-scoped.
/// </summary>
public class WasteLogEntry
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Branch the waste occurred at. Null = tenant-wide (single-branch tenants).</summary>
    public int? BranchId { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Units written off (always positive; stored as a positive count).</summary>
    public int Quantity { get; set; }

    public WasteReason Reason { get; set; } = WasteReason.Other;

    /// <summary>Estimated cost of the wasted stock at the time (Quantity × product cost).</summary>
    public decimal EstimatedCost { get; set; }

    public string? Notes { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
