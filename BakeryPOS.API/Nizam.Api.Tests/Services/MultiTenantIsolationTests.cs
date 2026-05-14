using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Verifies the architectural promise of the multi-tenant foundation: data written
/// while one tenant is in scope cannot be read while a different tenant is in scope.
/// </summary>
public class MultiTenantIsolationTests
{
    [Fact]
    public async Task Query_ScopedByTenant_DoesNotSeeOtherTenantsData()
    {
        // Same backing in-memory database across both contexts so we can verify isolation
        // happens at the query-filter level, not at the storage level.
        var dbName = $"isolation-{Guid.NewGuid():N}";

        DbContextOptions<AppDbContext> OptionsFor() => new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        // Seed under tenant 1.
        await using (var ctxT1 = new AppDbContext(OptionsFor(), new AmbientTenant(1)))
        {
            ctxT1.Categories.Add(new Category { Name = "Tenant-1 Cakes" });
            ctxT1.Categories.Add(new Category { Name = "Tenant-1 Drinks" });
            await ctxT1.SaveChangesAsync();
        }

        // Seed under tenant 2.
        await using (var ctxT2 = new AppDbContext(OptionsFor(), new AmbientTenant(2)))
        {
            ctxT2.Categories.Add(new Category { Name = "Tenant-2 Pastries" });
            await ctxT2.SaveChangesAsync();
        }

        // Tenant 1 sees only its own rows.
        await using (var ctxT1 = new AppDbContext(OptionsFor(), new AmbientTenant(1)))
        {
            var names = await ctxT1.Categories.Select(c => c.Name).ToListAsync();
            Assert.Equal(2, names.Count);
            Assert.All(names, n => Assert.StartsWith("Tenant-1", n));
        }

        // Tenant 2 sees only its own row.
        await using (var ctxT2 = new AppDbContext(OptionsFor(), new AmbientTenant(2)))
        {
            var names = await ctxT2.Categories.Select(c => c.Name).ToListAsync();
            Assert.Single(names);
            Assert.StartsWith("Tenant-2", names[0]);
        }

        // Out-of-band context (null tenant) bypasses the filter — used by the seeder and by
        // any background job that explicitly needs cross-tenant access. Acts as a safety net:
        // if a code path forgets to set a tenant, the filter returns NOTHING (empty), never
        // accidentally exposing cross-tenant data.
        await using (var ctxNull = new AppDbContext(OptionsFor(), new AmbientTenant(null)))
        {
            var all = await ctxNull.Categories.IgnoreQueryFilters().ToListAsync();
            Assert.Equal(3, all.Count);
        }
    }

    [Fact]
    public async Task SaveChanges_AutoStampsTenantId_OnNewEntities()
    {
        await using var ctx = TestContextFactory.Create(tenantId: 42);

        // Caller forgets to set TenantId — auto-stamp should fill it in from CurrentTenant.
        ctx.Categories.Add(new Category { Name = "Auto-Stamped" });
        await ctx.SaveChangesAsync();

        var stamped = await ctx.Categories.IgnoreQueryFilters().SingleAsync(c => c.Name == "Auto-Stamped");
        Assert.Equal(42, stamped.TenantId);
    }
}
