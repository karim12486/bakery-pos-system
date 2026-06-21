using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services.Plans;

/// <summary>
/// Manages per-tenant feature overrides — the add-on grant + sales escape-hatch mechanism.
/// Operates by EXPLICIT tenant id (cross-tenant master data, like the plan catalog), so it
/// uses <c>IgnoreQueryFilters</c>. Intended for the super-admin portal (3.4) and the billing
/// flow (4.5); not exposed to tenant-level callers (a tenant must not grant itself paid
/// features for free).
///
/// <para>Every mutation busts the <see cref="IPlanService"/> cache for the affected tenant so
/// the new entitlement set takes effect on the next request.</para>
/// </summary>
public interface ITenantFeatureOverrideService
{
    Task<IReadOnlyList<TenantFeatureOverride>> ListAsync(int tenantId, CancellationToken ct);

    /// <summary>Grant (enabled=true) or revoke (enabled=false) a feature for a tenant. Upserts
    /// the single row per (tenant, feature).</summary>
    Task SetAsync(int tenantId, string featureKey, bool enabled, DateTime? expiresAt, string? reason, CancellationToken ct);

    /// <summary>Removes the override row entirely (reverts to the plan's default for that feature).</summary>
    Task ClearAsync(int tenantId, string featureKey, CancellationToken ct);
}

public sealed class TenantFeatureOverrideService : ITenantFeatureOverrideService
{
    private readonly AppDbContext _context;
    private readonly IPlanService _plans;

    public TenantFeatureOverrideService(AppDbContext context, IPlanService plans)
    {
        _context = context;
        _plans = plans;
    }

    public async Task<IReadOnlyList<TenantFeatureOverride>> ListAsync(int tenantId, CancellationToken ct)
        => await _context.TenantFeatureOverrides
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.FeatureKey)
            .ToListAsync(ct);

    public async Task SetAsync(int tenantId, string featureKey, bool enabled, DateTime? expiresAt, string? reason, CancellationToken ct)
    {
        var existing = await _context.TenantFeatureOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.FeatureKey == featureKey, ct);

        var now = DateTime.UtcNow;
        if (existing == null)
        {
            _context.TenantFeatureOverrides.Add(new TenantFeatureOverride
            {
                TenantId = tenantId,
                FeatureKey = featureKey,
                Enabled = enabled,
                ExpiresAt = expiresAt,
                Reason = reason,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Enabled = enabled;
            existing.ExpiresAt = expiresAt;
            existing.Reason = reason;
            existing.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(ct);
        _plans.InvalidateCache(tenantId);
    }

    public async Task ClearAsync(int tenantId, string featureKey, CancellationToken ct)
    {
        var existing = await _context.TenantFeatureOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.FeatureKey == featureKey, ct);
        if (existing == null) return;

        _context.TenantFeatureOverrides.Remove(existing);
        await _context.SaveChangesAsync(ct);
        _plans.InvalidateCache(tenantId);
    }
}
