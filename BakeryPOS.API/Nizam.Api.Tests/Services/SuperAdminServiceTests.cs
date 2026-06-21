using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Nizam.Api.Services.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SuperAdminService"/> — login, cross-tenant listing, status
/// changes, and plan changes (which bust the plan cache).
/// </summary>
public class SuperAdminServiceTests
{
    private static readonly IPasswordService Passwords = new PasswordService();

    private static DbContextOptions<AppDbContext> Options(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private sealed class StubToken : ITokenService
    {
        public string CreateToken(User user, int? branchId = null) => "t";
        public string CreateSuperAdminToken(SuperAdmin admin) => $"super-{admin.Username}";
    }

    private static async Task<string> SeedAsync()
    {
        var dbName = $"super-{Guid.NewGuid():N}";
        await using var ctx = new AppDbContext(Options(dbName), new AmbientTenant(null));
        ctx.SuperAdmins.Add(new SuperAdmin
        {
            Username = "superadmin", PasswordHash = Passwords.HashPassword("secret123"),
            FullName = "Platform Admin", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        ctx.Plans.AddRange(
            new Plan { Code = "starter", Name = "Starter", MonthlyPriceEgp = 999, AnnualPriceEgp = 9990, IsActive = true },
            new Plan { Code = "growth", Name = "Growth", MonthlyPriceEgp = 1999, AnnualPriceEgp = 19990, IsActive = true });
        ctx.Tenants.AddRange(
            new Tenant { Id = 1, Name = "Alpha Café", Slug = "alpha", PlanCode = "starter", BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow },
            new Tenant { Id = 2, Name = "Beta Bakery", Slug = "beta", PlanCode = "growth", BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        return dbName;
    }

    private static (SuperAdminService Svc, IMemoryCache Cache, string Db) Build(string dbName)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var ctx = new AppDbContext(Options(dbName), new AmbientTenant(null));
        var plans = new PlanService(new AppDbContext(Options(dbName), new AmbientTenant(null)),
            new AmbientTenant(null), cache, NullLogger<PlanService>.Instance);
        return (new SuperAdminService(ctx, Passwords, new StubToken(), plans), cache, dbName);
    }

    [Fact]
    public async Task Login_Valid_ReturnsToken()
    {
        var (svc, _, _) = Build(await SeedAsync());
        var dto = await svc.LoginAsync(new SuperAdminLoginDto { Username = "superadmin", Password = "secret123" }, CancellationToken.None);
        Assert.Equal("super-superadmin", dto.Token);
        Assert.Equal("Platform Admin", dto.FullName);
    }

    [Fact]
    public async Task Login_WrongPassword_Throws401()
    {
        var (svc, _, _) = Build(await SeedAsync());
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.LoginAsync(new SuperAdminLoginDto { Username = "superadmin", Password = "nope" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task ListTenants_ReturnsAll_WithCounts_AndSearch()
    {
        var (svc, _, _) = Build(await SeedAsync());

        var all = await svc.ListTenantsAsync(null, CancellationToken.None);
        Assert.Equal(2, all.Count);

        var filtered = await svc.ListTenantsAsync("bakery", CancellationToken.None);
        var one = Assert.Single(filtered);
        Assert.Equal("Beta Bakery", one.Name);
    }

    [Fact]
    public async Task Suspend_And_Reactivate_ChangesStatus()
    {
        var db = await SeedAsync();
        var (svc, _, _) = Build(db);

        await svc.SetStatusAsync(1, "suspended", CancellationToken.None);
        await using (var ctx = new AppDbContext(Options(db), new AmbientTenant(null)))
            Assert.Equal("suspended", (await ctx.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == 1)).Status);

        await svc.SetStatusAsync(1, "active", CancellationToken.None);
        await using (var ctx = new AppDbContext(Options(db), new AmbientTenant(null)))
            Assert.Equal("active", (await ctx.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == 1)).Status);
    }

    [Fact]
    public async Task SetStatus_Invalid_Throws()
    {
        var (svc, _, _) = Build(await SeedAsync());
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.SetStatusAsync(1, "frozen", CancellationToken.None));
        Assert.Equal("ERR_INVALID_STATUS", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePlan_Valid_UpdatesAndBustsCache()
    {
        var db = await SeedAsync();
        var (svc, cache, _) = Build(db);
        cache.Set("plan:1", "stale"); // simulate a cached snapshot for tenant 1

        await svc.ChangePlanAsync(1, "growth", CancellationToken.None);

        await using (var ctx = new AppDbContext(Options(db), new AmbientTenant(null)))
            Assert.Equal("growth", (await ctx.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == 1)).PlanCode);
        Assert.False(cache.TryGetValue("plan:1", out _)); // cache invalidated
    }

    [Fact]
    public async Task ChangePlan_UnknownPlan_Throws()
    {
        var (svc, _, _) = Build(await SeedAsync());
        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.ChangePlanAsync(1, "enterprise", CancellationToken.None));
    }

    [Fact]
    public async Task GetTenant_ReturnsDetailWithOverrides()
    {
        var db = await SeedAsync();
        await using (var ctx = new AppDbContext(Options(db), new AmbientTenant(null)))
        {
            ctx.TenantFeatureOverrides.Add(new TenantFeatureOverride
            {
                TenantId = 1, FeatureKey = "loyalty", Enabled = true, Reason = "add-on", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }
        var (svc, _, _) = Build(db);

        var detail = await svc.GetTenantAsync(1, CancellationToken.None);
        Assert.Equal("Alpha Café", detail.Tenant.Name);
        var ovr = Assert.Single(detail.FeatureOverrides);
        Assert.Equal("loyalty", ovr.FeatureKey);
    }
}
