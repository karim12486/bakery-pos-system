namespace Nizam.Api.Core.Entities;

/// <summary>
/// A vendor the tenant buys stock from (Phase 3.7 / inventory_ops). Referenced by purchase
/// orders. Tenant-scoped; soft-deactivated via <see cref="IsActive"/> rather than deleted so
/// historical POs keep their supplier reference.
/// </summary>
public class Supplier
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
