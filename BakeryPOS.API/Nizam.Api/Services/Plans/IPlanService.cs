using Nizam.Api.Core.Entities;

namespace Nizam.Api.Services.Plans;

/// <summary>
/// Read API for the current tenant's subscription plan. Every plan-gated controller and
/// limit-aware service create reads from this. Hot path — cached with a per-tenant
/// snapshot keyed on tenant id; sub-millisecond after the first call within the TTL.
///
/// Caching strategy:
/// <list type="bullet">
///   <item>Cache key: <c>plan:{tenantId}</c></item>
///   <item>Sliding TTL: 5 minutes</item>
///   <item>Snapshot contains: plan code, granted features (HashSet&lt;string&gt;),
///         numeric limits (Dictionary&lt;string, int&gt;)</item>
///   <item>Limit value <c>-1</c> = unlimited (matches the seeder convention)</item>
/// </list>
///
/// Cache invalidation:
/// <list type="bullet">
///   <item>Automatic on TTL expiry (5 min, sliding)</item>
///   <item>Manual via <see cref="InvalidateCache"/> — called by the super-admin portal
///         when a tenant's plan changes</item>
/// </list>
///
/// Anonymous calls (<see cref="Common.Tenancy.ICurrentTenant.TenantId"/> is null):
/// <see cref="HasFeatureAsync"/> returns false, <see cref="GetLimitAsync"/> returns 0.
/// <see cref="EnsureWithinLimitAsync"/> throws to prevent silent feature grants outside
/// any tenant context.
/// </summary>
public interface IPlanService
{
    /// <summary>Returns the current tenant's plan with features/limits eagerly loaded,
    /// or null if no tenant is in scope.</summary>
    Task<Plan?> GetCurrentPlanAsync(CancellationToken ct = default);

    /// <summary>True if the current tenant's plan grants the given feature key. False if
    /// no tenant is in scope or the feature is not granted.</summary>
    Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default);

    /// <summary>Returns the numeric limit for the given key on the current tenant's plan.
    /// Returns 0 if no tenant is in scope. Returns -1 (unlimited) for limits explicitly
    /// seeded as unlimited (e.g. <c>max_products</c> on Pro).</summary>
    Task<int> GetLimitAsync(string limitKey, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="Common.Errors.PlanLimitExceededException"/> if <paramref name="currentCount"/>
    /// would meet or exceed the configured limit (i.e. you're about to create the
    /// <c>currentCount + 1</c>-th entity and the limit is <c>currentCount</c>).
    ///
    /// <para>Convention: pass the COUNT BEFORE adding the new item. Example: to add a
    /// branch when 3 exist, call <c>EnsureWithinLimitAsync("max_branches", 3)</c>; it throws
    /// if 3 ≥ max_branches.</para>
    ///
    /// Unlimited (<c>-1</c>) always passes. No tenant in scope → throws (anonymous code
    /// must not silently bypass limits).
    /// </summary>
    Task EnsureWithinLimitAsync(string limitKey, int currentCount, CancellationToken ct = default);

    /// <summary>Returns every feature key granted to the current tenant's plan, for the UI
    /// to render upgrade prompts and surface available features. Empty if no tenant.</summary>
    Task<IReadOnlyList<string>> GetGrantedFeaturesAsync(CancellationToken ct = default);

    /// <summary>Drops the cached snapshot for the given tenant. Called by the super-admin
    /// portal when a tenant's plan changes so the new entitlement set takes effect on the
    /// next request instead of waiting 5 minutes for TTL expiry.</summary>
    void InvalidateCache(int tenantId);
}
