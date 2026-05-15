using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.Services.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PlanService"/> — the cached read API every plan-gated controller
/// will call. Verifies feature lookups, limit math, cache behaviour, and tenant isolation.
/// </summary>
public class PlanServiceTests
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    // ----- Helpers --------------------------------------------------------------------

    private static DbContextOptions<AppDbContext> SharedOptions(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    /// <summary>Seeds a Plan + features + limits and a Tenant on that plan. Returns the DB name.</summary>
    private static async Task<string> SeedAsync(
        string planCode,
        string[] features,
        (string Key, int Value)[] limits,
        int tenantId = TenantA)
    {
        var dbName = $"plan-svc-{Guid.NewGuid():N}";
        await using var ctx = new AppDbContext(SharedOptions(dbName), new AmbientTenant(null));

        var plan = new Plan
        {
            Code = planCode,
            Name = planCode,
            Description = "",
            MonthlyPriceEgp = 1000,
            AnnualPriceEgp = 10000,
            IsActive = true,
        };
        plan.Features = features.Select(f => new PlanFeature { PlanCode = planCode, FeatureKey = f, Plan = plan }).ToList();
        plan.Limits = limits.Select(l => new PlanLimit { PlanCode = planCode, LimitKey = l.Key, Value = l.Value, Plan = plan }).ToList();
        ctx.Plans.Add(plan);

        ctx.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Slug = $"test-{tenantId}",
            PlanCode = planCode,
            BillingCycle = "monthly",
            Currency = "EGP",
            Locale = "ar-EG",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return dbName;
    }

    private static PlanService BuildService(string dbName, int? tenantId, IMemoryCache? cache = null)
    {
        var ctx = new AppDbContext(SharedOptions(dbName), new AmbientTenant(tenantId));
        cache ??= new MemoryCache(new MemoryCacheOptions());
        return new PlanService(ctx, new AmbientTenant(tenantId), cache, NullLogger<PlanService>.Instance);
    }

    // ----- Feature checks -------------------------------------------------------------

    [Fact]
    public async Task HasFeatureAsync_ReturnsTrue_WhenFeatureGranted()
    {
        var db = await SeedAsync("growth",
            features: new[] { "modifiers", "loyalty" },
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, TenantA);

        Assert.True(await svc.HasFeatureAsync("modifiers"));
        Assert.True(await svc.HasFeatureAsync("loyalty"));
    }

    [Fact]
    public async Task HasFeatureAsync_ReturnsFalse_WhenFeatureNotGranted()
    {
        var db = await SeedAsync("starter",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 1) });
        var svc = BuildService(db, TenantA);

        Assert.False(await svc.HasFeatureAsync("modifiers"));
        Assert.False(await svc.HasFeatureAsync("kds"));
    }

    [Fact]
    public async Task HasFeatureAsync_ReturnsFalse_WhenNoTenantInScope()
    {
        var db = await SeedAsync("growth",
            features: new[] { "modifiers" },
            limits: Array.Empty<(string, int)>());
        var svc = BuildService(db, tenantId: null);

        Assert.False(await svc.HasFeatureAsync("modifiers"));
    }

    // ----- Limit checks ---------------------------------------------------------------

    [Fact]
    public async Task GetLimitAsync_ReturnsConfiguredValue()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 3), ("max_users", 15) });
        var svc = BuildService(db, TenantA);

        Assert.Equal(3, await svc.GetLimitAsync("max_branches"));
        Assert.Equal(15, await svc.GetLimitAsync("max_users"));
    }

    [Fact]
    public async Task GetLimitAsync_ReturnsZero_WhenNoTenantInScope()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, tenantId: null);

        Assert.Equal(0, await svc.GetLimitAsync("max_branches"));
    }

    [Fact]
    public async Task EnsureWithinLimitAsync_DoesNotThrow_WhenUnderLimit()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, TenantA);

        // 2 branches exist, limit 3 — adding the 3rd is fine.
        await svc.EnsureWithinLimitAsync("max_branches", currentCount: 2);
    }

    [Fact]
    public async Task EnsureWithinLimitAsync_Throws_WhenAtLimit()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, TenantA);

        // 3 branches exist, limit 3 — adding the 4th would push over.
        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => svc.EnsureWithinLimitAsync("max_branches", currentCount: 3));

        Assert.Equal("max_branches", ex.LimitKey);
        Assert.Equal(3, ex.CurrentCount);
        Assert.Equal(3, ex.MaxAllowed);
        Assert.Equal("growth", ex.CurrentPlan);
        Assert.Equal(402, ex.StatusCode);
        Assert.Equal("ERR_PLAN_LIMIT_EXCEEDED", ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureWithinLimitAsync_Throws_WhenOverLimit()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_users", 15) });
        var svc = BuildService(db, TenantA);

        await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => svc.EnsureWithinLimitAsync("max_users", currentCount: 20));
    }

    [Fact]
    public async Task EnsureWithinLimitAsync_NeverThrows_WhenLimitIsUnlimited()
    {
        var db = await SeedAsync("pro",
            features: Array.Empty<string>(),
            limits: new[] { ("max_users", -1) });
        var svc = BuildService(db, TenantA);

        await svc.EnsureWithinLimitAsync("max_users", currentCount: 99_999);
    }

    [Fact]
    public async Task EnsureWithinLimitAsync_NoLimitConfigured_TreatedAsUnlimited()
    {
        // Documented behaviour: a missing limit key means unlimited. Lets us add new
        // limit keys (e.g. max_modifiers) without backfilling every legacy plan.
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, TenantA);

        await svc.EnsureWithinLimitAsync("max_modifiers", currentCount: 50);
    }

    [Fact]
    public async Task EnsureWithinLimitAsync_Throws_WhenNoTenantInScope()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, tenantId: null);

        // Anonymous code must not silently bypass limits.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.EnsureWithinLimitAsync("max_branches", currentCount: 0));
    }

    // ----- Granted features list ------------------------------------------------------

    [Fact]
    public async Task GetGrantedFeaturesAsync_ReturnsAllFeatures()
    {
        var features = new[] { "modifiers", "loyalty", "promotions" };
        var db = await SeedAsync("growth", features: features, limits: Array.Empty<(string, int)>());
        var svc = BuildService(db, TenantA);

        var granted = await svc.GetGrantedFeaturesAsync();

        Assert.Equal(features.Length, granted.Count);
        foreach (var f in features) Assert.Contains(f, granted);
    }

    [Fact]
    public async Task GetGrantedFeaturesAsync_ReturnsEmpty_WhenNoTenantInScope()
    {
        var db = await SeedAsync("growth",
            features: new[] { "modifiers" },
            limits: Array.Empty<(string, int)>());
        var svc = BuildService(db, tenantId: null);

        Assert.Empty(await svc.GetGrantedFeaturesAsync());
    }

    // ----- Cache behaviour ------------------------------------------------------------

    [Fact]
    public async Task Second_Call_Hits_Cache_Not_Database()
    {
        // Two calls — second should not touch the DB. Verify by mutating the DB between
        // calls and asserting the second call still returns the pre-mutation snapshot.
        var dbName = await SeedAsync("growth",
            features: new[] { "modifiers" },
            limits: new[] { ("max_branches", 3) });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = BuildService(dbName, TenantA, cache);

        var first = await svc.HasFeatureAsync("modifiers");
        Assert.True(first);

        // Mutate DB out-of-band: revoke the feature directly. Without a cache, the next
        // call would return false. With cache, it should still return true (cached snapshot).
        await using (var direct = new AppDbContext(SharedOptions(dbName), new AmbientTenant(null)))
        {
            var stale = await direct.PlanFeatures.FirstOrDefaultAsync(f => f.PlanCode == "growth" && f.FeatureKey == "modifiers");
            if (stale != null) direct.PlanFeatures.Remove(stale);
            await direct.SaveChangesAsync();
        }

        Assert.True(await svc.HasFeatureAsync("modifiers"));
    }

    [Fact]
    public async Task InvalidateCache_Forces_Refetch_From_Database()
    {
        var dbName = await SeedAsync("growth",
            features: new[] { "modifiers" },
            limits: Array.Empty<(string, int)>());

        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = BuildService(dbName, TenantA, cache);

        // Warm cache.
        Assert.True(await svc.HasFeatureAsync("modifiers"));

        // Out-of-band mutation: revoke the feature.
        await using (var direct = new AppDbContext(SharedOptions(dbName), new AmbientTenant(null)))
        {
            var stale = await direct.PlanFeatures.FirstAsync(f => f.PlanCode == "growth" && f.FeatureKey == "modifiers");
            direct.PlanFeatures.Remove(stale);
            await direct.SaveChangesAsync();
        }

        // Bust the cache — now the next call must refetch and reflect the revocation.
        svc.InvalidateCache(TenantA);

        // New service instance to drop any per-request EF tracking (mimics next HTTP request).
        var svc2 = BuildService(dbName, TenantA, cache);
        Assert.False(await svc2.HasFeatureAsync("modifiers"));
    }

    [Fact]
    public async Task Cache_Is_Scoped_Per_Tenant()
    {
        // Build a single shared DB with two tenants on different plans. Verify each
        // service instance, even sharing the cache, sees only its own tenant's entitlements.
        var dbName = $"plan-svc-isolation-{Guid.NewGuid():N}";

        await using (var seed = new AppDbContext(SharedOptions(dbName), new AmbientTenant(null)))
        {
            var starter = new Plan { Code = "starter", Name = "Starter", MonthlyPriceEgp = 999, AnnualPriceEgp = 9990 };
            starter.Features = new List<PlanFeature>();
            starter.Limits = new List<PlanLimit> { new() { PlanCode = "starter", LimitKey = "max_branches", Value = 1 } };

            var growth = new Plan { Code = "growth", Name = "Growth", MonthlyPriceEgp = 1999, AnnualPriceEgp = 19990 };
            growth.Features = new List<PlanFeature> { new() { PlanCode = "growth", FeatureKey = "modifiers" } };
            growth.Limits = new List<PlanLimit> { new() { PlanCode = "growth", LimitKey = "max_branches", Value = 3 } };

            seed.Plans.AddRange(starter, growth);

            seed.Tenants.AddRange(
                new Tenant { Id = TenantA, Name = "A", Slug = "tenant-a", PlanCode = "starter", BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow },
                new Tenant { Id = TenantB, Name = "B", Slug = "tenant-b", PlanCode = "growth", BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        var sharedCache = new MemoryCache(new MemoryCacheOptions());
        var svcA = BuildService(dbName, TenantA, sharedCache);
        var svcB = BuildService(dbName, TenantB, sharedCache);

        Assert.False(await svcA.HasFeatureAsync("modifiers"));
        Assert.True(await svcB.HasFeatureAsync("modifiers"));

        Assert.Equal(1, await svcA.GetLimitAsync("max_branches"));
        Assert.Equal(3, await svcB.GetLimitAsync("max_branches"));
    }

    // ----- GetCurrentPlanAsync --------------------------------------------------------

    [Fact]
    public async Task GetCurrentPlanAsync_ReturnsPlanWithFeaturesAndLimits()
    {
        var db = await SeedAsync("growth",
            features: new[] { "modifiers", "loyalty" },
            limits: new[] { ("max_branches", 3) });
        var svc = BuildService(db, TenantA);

        var plan = await svc.GetCurrentPlanAsync();

        Assert.NotNull(plan);
        Assert.Equal("growth", plan!.Code);
    }

    [Fact]
    public async Task GetCurrentPlanAsync_ReturnsNull_WhenNoTenantInScope()
    {
        var db = await SeedAsync("growth",
            features: Array.Empty<string>(),
            limits: Array.Empty<(string, int)>());
        var svc = BuildService(db, tenantId: null);

        Assert.Null(await svc.GetCurrentPlanAsync());
    }
}
