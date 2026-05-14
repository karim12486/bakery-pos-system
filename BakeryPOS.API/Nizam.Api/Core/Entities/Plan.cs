namespace Nizam.Api.Core.Entities;

/// <summary>
/// Subscription plan master record (tenant-AGNOSTIC — every tenant sees the same plan catalog).
/// Codes are stable and case-sensitive: <c>starter</c>, <c>growth</c>, <c>pro</c>.
/// Prices are in EGP, the launch currency. Multi-currency pricing is a future concern.
/// </summary>
public class Plan
{
    /// <summary>PK — stable lowercase code: <c>starter</c>, <c>growth</c>, <c>pro</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name shown in pricing UI and invoices.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Marketing description shown on the pricing page.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Monthly price in EGP. Stored as decimal(10,2).</summary>
    public decimal MonthlyPriceEgp { get; set; }

    /// <summary>Annual price in EGP (typically 10× monthly = 2 months free).</summary>
    public decimal AnnualPriceEgp { get; set; }

    /// <summary>Lower value = displayed earlier on pricing page.</summary>
    public int SortOrder { get; set; }

    /// <summary>Inactive plans cannot be subscribed to but existing tenants keep their grandfathered terms.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Hidden plans (e.g. legacy / negotiated) don't appear on the public pricing page.</summary>
    public bool IsPubliclyVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
    public ICollection<PlanLimit> Limits { get; set; } = new List<PlanLimit>();
}
