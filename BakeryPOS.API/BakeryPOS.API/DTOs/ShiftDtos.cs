using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs;

public class OpenShiftDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    /// <summary>Cash on hand at the start of the shift (EGP, decimal). Required by every
    /// real POS for end-of-day variance accountability.</summary>
    [Required]
    [Range(0, 1_000_000)]
    public decimal OpeningFloat { get; set; }
}

public class CloseShiftDto
{
    [Required]
    [Range(0, 1_000_000)]
    public decimal ClosingCount { get; set; }

    [StringLength(500)]
    public string? VarianceNotes { get; set; }
}

public class ShiftDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? ClosingCount { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Variance { get; set; }
    public string? VarianceNotes { get; set; }
    public bool IsOpen => ClosedAt == null;
}

/// <summary>Z-report — end-of-shift totals. Generated at shift close.</summary>
public class ZReportDto
{
    public int ShiftId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime ClosedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;

    public decimal OpeningFloat { get; set; }
    public decimal ClosingCount { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal Variance { get; set; }

    public int OrderCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal NetSales { get; set; }

    public decimal CashTaken { get; set; }
    public decimal CardTaken { get; set; }
    public decimal TabExtended { get; set; }
}
