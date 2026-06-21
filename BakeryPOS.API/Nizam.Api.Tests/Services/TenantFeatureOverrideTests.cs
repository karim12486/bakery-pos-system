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
/// Tests for the tenant-feature-override mechanism: the service that manages overrides and the
/// PlanService integration that folds them into the effective feature set.
/// </summary>
public class TenantFeatureOverrideTests
{
    private const int TenantId = 1;

    private static DbContextOptions<AppDbContext> Options(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    /// <summary>Seeds a Starter plan (no loyalty) + a tenant on it. Returns the shared db name.</summary>
    private static async Task<string> SeedAsync()
    {
        var dbName = $"override-{Guid.NewGuid():N}";
        await using var ctx = new AppDbContext(Options(dbName), new AmbientTenant(null));
        var plan = new Plan { Code = "starter", Name = "Starter", MonthlyPriceEgp = 999, AnnualPriceEgp = 9990 };
        plan.Features = new List<PlanFeature>(); // Starter grants no premium features
        plan.Limits = new List<PlanLimit>();
        ctx.Plans.Add(plan);
        ctx.Tenants.Add(new Tenant
        {
            Id = TenantId, Name = "Kiosk", Slug = "kiosk", PlanCode = "starter",
            BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return dbName;
    }

    private static (PlanService Plan, TenantFeatureOverrideService Override, IMemoryCache Cache) BuildServices(string dbName)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        // Each service gets its own context but they share the in-memory DB + the cache.
        var planCtx = new AppDbContext(Options(dbName), new AmbientTenant(TenantId));
        var plan = new PlanService(planCtx, new AmbientTenant(TenantId), cache, NullLogger<PlanService>.Instance);
        var ovrCtx = new AppDbContext(Options(dbName), new AmbientTenant(TenantId));
        var ovr = new TenantFeatureOverrideService(ovrCtx, plan);
        return (plan, ovr, cache);
    }

    [Fact]
    public async Task Override_GrantsFeature_NotInPlan()
    {
        var db = await SeedAsync();
        var (plan, ovr, _) = BuildServices(db);

        Assert.False(await plan.HasFeatureAsync("loyalty")); // Starter doesn't include it

        await ovr.SetAsync(TenantId, "loyalty", enabled: true, expiresAt: null, reason: "add-on", CancellationToken.None);

        // SetAsync invalidated the cache → next check re-reads with the override applied.
        Assert.True(await plan.HasFeatureAsync("loyalty"));
    }

    [Fact]
    public async Task Override_Disabled_RevokesPlanFeature()
    {
        var dbName = $"override-rev-{Guid.NewGuid():N}";
        await using (var ctx = new AppDbContext(Options(dbName), new AmbientTenant(null)))
        {
            // Growth plan that DOES include modifiers.
            var plan = new Plan { Code = "growth", Name = "Growth", MonthlyPriceEgp = 1999, AnnualPriceEgp = 19990 };
            plan.Features = new List<PlanFeature> { new() { PlanCode = "growth", FeatureKey = "modifiers" } };
            plan.Limits = new List<PlanLimit>();
            ctx.Plans.Add(plan);
            ctx.Tenants.Add(new Tenant
            {
                Id = TenantId, Name = "Café", Slug = "cafe", PlanCode = "growth",
                BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }
        var (planSvc, ovr, _) = BuildServices(dbName);

        Assert.True(await planSvc.HasFeatureAsync("modifiers"));

        await ovr.SetAsync(TenantId, "modifiers", enabled: false, expiresAt: null, reason: "dispute", CancellationToken.None);
        Assert.False(await planSvc.HasFeatureAsync("modifiers")); // negative override wins
    }

    [Fact]
    public async Task Override_Expired_Ignored()
    {
        var db = await SeedAsync();
        var (plan, ovr, _) = BuildServices(db);

        await ovr.SetAsync(TenantId, "loyalty", enabled: true,
            expiresAt: DateTime.UtcNow.AddMinutes(-1), reason: "lapsed", CancellationToken.None);

        Assert.False(await plan.HasFeatureAsync("loyalty")); // expired → not applied
    }

    [Fact]
    public async Task Clear_RemovesOverride_RevertsToPlan()
    {
        var db = await SeedAsync();
        var (plan, ovr, _) = BuildServices(db);

        await ovr.SetAsync(TenantId, "loyalty", true, null, "add-on", CancellationToken.None);
        Assert.True(await plan.HasFeatureAsync("loyalty"));

        await ovr.ClearAsync(TenantId, "loyalty", CancellationToken.None);
        Assert.False(await plan.HasFeatureAsync("loyalty")); // back to Starter default
    }

    [Fact]
    public async Task Set_Upserts_SingleRowPerFeature()
    {
        var db = await SeedAsync();
        var (_, ovr, _) = BuildServices(db);

        await ovr.SetAsync(TenantId, "loyalty", true, null, "first", CancellationToken.None);
        await ovr.SetAsync(TenantId, "loyalty", true, null, "updated", CancellationToken.None);

        var list = await ovr.ListAsync(TenantId, CancellationToken.None);
        var row = Assert.Single(list);
        Assert.Equal("updated", row.Reason); // upserted, not duplicated
    }
}
