namespace Nizam.Api.Core.Entities;

/// <summary>
/// A preparation station type in the tenant's kitchen — Hot Kitchen, Cold Kitchen, Bar,
/// Bakery, Grill, etc. Products route to a station via their <see cref="Category"/>; each
/// physical branch has a KDS screen per station.
///
/// <para><b>Tenant-scoped, not per-branch</b>: the station is a logical type ("Hot Kitchen")
/// shared across branches, matching how <see cref="Category"/> is tenant-scoped. The KDS hub
/// scopes screens by <c>{tenantId}:kds:{branchId}:{stationId}</c> — the branch comes from the
/// order, so the same station type drives a screen at every branch independently. Per-branch
/// routing overrides are Phase B.5.</para>
/// </summary>
public class KitchenStation
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Display label (e.g. "Hot Kitchen", "Bar"). Unique per tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower sort_order = listed earlier in the station picker / expo view.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
