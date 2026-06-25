using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A branch-to-branch stock transfer (Phase 3.7 / inventory_ops). Draft → Sent → Received
/// (or Cancelled). Sending writes a <c>TransferOut</c> movement (tagged to the source branch);
/// receiving writes a <c>TransferIn</c> movement (tagged to the destination branch).
///
/// <para>Stock is currently tracked tenant-wide on <c>Product.StockQuantity</c> (not per branch),
/// so a transfer is net-zero on the product total — the value is the branch-tagged audit trail
/// of what moved where. Per-branch stock levels are a follow-up (logged in the test backlog).</para>
/// </summary>
public class StockTransfer
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }

    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;

    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    public List<StockTransferItem> Items { get; set; } = new();
}

/// <summary>A single product line on a <see cref="StockTransfer"/>.</summary>
public class StockTransferItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
}
