using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Nizam.Api.Services.Plans;

public sealed class PlanService : IPlanService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "plan:";

    private readonly AppDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlanService> _logger;

    public PlanService(
        AppDbContext context,
        ICurrentTenant currentTenant,
        IMemoryCache cache,
        ILogger<PlanService> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Plan?> GetCurrentPlanAsync(CancellationToken ct = default)
    {
        if (_currentTenant.TenantId is not int tenantId) return null;
        var snapshot = await GetSnapshotAsync(tenantId, ct);
        return snapshot?.Plan;
    }

    public async Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default)
    {
        if (_currentTenant.TenantId is not int tenantId) return false;
        var snapshot = await GetSnapshotAsync(tenantId, ct);
        return snapshot != null && snapshot.Features.Contains(featureKey);
    }

    public async Task<int> GetLimitAsync(string limitKey, CancellationToken ct = default)
    {
        if (_currentTenant.TenantId is not int tenantId) return 0;
        var snapshot = await GetSnapshotAsync(tenantId, ct);
        if (snapshot == null) return 0;
        return snapshot.Limits.TryGetValue(limitKey, out var value) ? value : 0;
    }

    public async Task EnsureWithinLimitAsync(string limitKey, int currentCount, CancellationToken ct = default)
    {
        if (_currentTenant.TenantId is not int tenantId)
        {
            // Anonymous / cross-tenant code must not silently bypass limits.
            throw new InvalidOperationException(
                $"EnsureWithinLimitAsync('{limitKey}') called without a tenant in scope. " +
                "Background jobs and seeders must set a tenant before checking limits.");
        }

        var snapshot = await GetSnapshotAsync(tenantId, ct)
            ?? throw new InvalidOperationException(
                $"Tenant {tenantId} has no plan. Seed the plan catalog and assign Tenant.PlanCode.");

        if (!snapshot.Limits.TryGetValue(limitKey, out var max))
        {
            // No limit configured = treat as unlimited. Documented behavior so adding a new
            // limit key doesn't require backfilling every existing plan.
            return;
        }

        if (max == -1) return;          // explicit unlimited
        if (currentCount < max) return; // still room

        throw new PlanLimitExceededException(limitKey, currentCount, max, snapshot.PlanCode);
    }

    public async Task<IReadOnlyList<string>> GetGrantedFeaturesAsync(CancellationToken ct = default)
    {
        if (_currentTenant.TenantId is not int tenantId) return Array.Empty<string>();
        var snapshot = await GetSnapshotAsync(tenantId, ct);
        return snapshot?.Features.ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public void InvalidateCache(int tenantId)
    {
        _cache.Remove(CacheKeyPrefix + tenantId);
        _logger.LogInformation("Invalidated plan cache for tenant {TenantId}", tenantId);
    }

    // --- Internals ---

    private async Task<PlanSnapshot?> GetSnapshotAsync(int tenantId, CancellationToken ct)
    {
        var key = CacheKeyPrefix + tenantId;
        if (_cache.TryGetValue<PlanSnapshot>(key, out var cached) && cached != null)
        {
            return cached;
        }

        // Cache miss — single query, eager load plan + features + limits.
        // IgnoreQueryFilters because Tenants is unfiltered already, but Plan
        // is tenant-agnostic master data either way.
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Plan!)
                .ThenInclude(p => p.Features)
            .Include(t => t.Plan!)
                .ThenInclude(p => p.Limits)
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.PlanCode, t.Plan })
            .FirstOrDefaultAsync(ct);

        if (tenant?.Plan == null)
        {
            // Tenant exists without a plan, or tenant id is bogus. Log and return null so
            // HasFeature/GetLimit return safe defaults (false/0). EnsureWithinLimitAsync's
            // caller will get an InvalidOperationException via the null-coalesce.
            _logger.LogWarning("No plan resolved for tenant {TenantId}", tenantId);
            return null;
        }

        var snapshot = new PlanSnapshot(
            Plan: tenant.Plan,
            PlanCode: tenant.Plan.Code,
            Features: new HashSet<string>(tenant.Plan.Features.Select(f => f.FeatureKey), StringComparer.Ordinal),
            Limits: tenant.Plan.Limits.ToDictionary(l => l.LimitKey, l => l.Value, StringComparer.Ordinal));

        _cache.Set(key, snapshot, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheTtl,
            Size = 1, // for IMemoryCache size-limited scenarios; harmless if size limit isn't set
        });

        return snapshot;
    }

    /// <summary>Cache entry — immutable per build to avoid races on the hot path.</summary>
    private sealed record PlanSnapshot(
        Plan Plan,
        string PlanCode,
        HashSet<string> Features,
        Dictionary<string, int> Limits);
}
