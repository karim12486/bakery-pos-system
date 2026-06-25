using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs;

// ----------------------------- Suppliers -----------------------------

public class SupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierUpsertDto
{
    [Required, StringLength(160, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)] public string? ContactName { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [EmailAddress, StringLength(160)] public string? Email { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

// --------------------------- Purchase orders ---------------------------

public class PurchaseOrderLineInputDto
{
    [Required] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int QuantityOrdered { get; set; }
    [Range(0, double.MaxValue)] public decimal UnitCost { get; set; }
}

public class PurchaseOrderCreateDto
{
    [Required] public int SupplierId { get; set; }
    public int? BranchId { get; set; }
    [StringLength(80)] public string? Reference { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    public DateTime? ExpectedDate { get; set; }

    [MinLength(1)] public List<PurchaseOrderLineInputDto> Items { get; set; } = new();
}

/// <summary>Per-line received quantity at receive time. Omitted lines default to ordered qty.</summary>
public class PurchaseOrderReceiveLineDto
{
    [Required] public int ProductId { get; set; }
    [Range(0, int.MaxValue)] public int QuantityReceived { get; set; }
}

public class PurchaseOrderReceiveDto
{
    public List<PurchaseOrderReceiveLineDto> Lines { get; set; } = new();
}

public class PurchaseOrderLineDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchaseOrderDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public decimal Total { get; set; }
    public IReadOnlyList<PurchaseOrderLineDto> Items { get; set; } = Array.Empty<PurchaseOrderLineDto>();
}

// --------------------------- Stock transfers ---------------------------

public class StockTransferLineInputDto
{
    [Required] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}

public class StockTransferCreateDto
{
    [Required] public int FromBranchId { get; set; }
    [Required] public int ToBranchId { get; set; }
    [StringLength(80)] public string? Reference { get; set; }
    [StringLength(500)] public string? Notes { get; set; }

    [MinLength(1)] public List<StockTransferLineInputDto> Items { get; set; } = new();
}

public class StockTransferLineDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class StockTransferDto
{
    public int Id { get; set; }
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public IReadOnlyList<StockTransferLineDto> Items { get; set; } = Array.Empty<StockTransferLineDto>();
}

// ----------------------------- Waste log -----------------------------

public class WasteLogCreateDto
{
    [Required] public int ProductId { get; set; }
    public int? BranchId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    [Required] public WasteReason Reason { get; set; } = WasteReason.Other;
    [StringLength(500)] public string? Notes { get; set; }
}

public class WasteLogEntryDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
