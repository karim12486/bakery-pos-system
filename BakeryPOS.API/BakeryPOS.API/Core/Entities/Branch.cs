namespace BakeryPOS.API.Core.Entities;

/// <summary>
/// A physical location belonging to a <see cref="Tenant"/>. Operational events
/// (sales, stock movements, expenses, removal requests) are branch-scoped.
/// </summary>
public class Branch
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    /// <summary>IANA tz database id, e.g. <c>Africa/Cairo</c>. Used for shift-day boundaries.</summary>
    public string Timezone { get; set; } = "Africa/Cairo";

    /// <summary>Effective sales-tax rate at this branch (0..1). 0 = no tax.</summary>
    public decimal TaxRate { get; set; }

    /// <summary>Optional branch-specific currency override; falls back to <see cref="Tenant.Currency"/>.</summary>
    public string? Currency { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
