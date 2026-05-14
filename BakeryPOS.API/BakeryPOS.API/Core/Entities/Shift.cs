using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities;

/// <summary>
/// A cashier session — opened at the start of a workday with the opening cash float,
/// closed at the end with the closing count. All sales recorded during the session
/// reference this shift via <c>Order.ShiftId</c>, enabling end-of-day Z-reports
/// and cash-variance accountability.
///
/// Aligns with the Figma's Opening Cash Float screen. PIN-based auth is a separate
/// upcoming change — for now, an authenticated user opens their own shift.
/// </summary>
public class Shift
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Cash on hand at the start of the shift, as counted by the cashier.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OpeningFloat { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the shift is closed; null while open.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Cashier-reported count of the drawer at shift close.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ClosingCount { get; set; }

    /// <summary>Computed at close: OpeningFloat + cash sales − cash refunds. What the
    /// drawer SHOULD hold.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ExpectedCash { get; set; }

    /// <summary>ClosingCount − ExpectedCash. Positive = overage, negative = shortage.
    /// Persisted alongside the inputs so Z-report values don't drift if rates change.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Variance { get; set; }

    /// <summary>Optional reason for variance (broken bill, missing receipt, etc.).</summary>
    public string? VarianceNotes { get; set; }

    public bool IsOpen => ClosedAt == null;
}
