using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PublicMenuService"/> — anonymous menu resolution by slug, the
/// plan-feature gate, and active-only filtering.
/// </summary>
public class PublicMenuServiceTests
{
    private const int TenantId = TestContextFactory.DefaultTenantId; // 1
    private const string Slug = "acme";

    private static PublicMenuService Build(AppDbContext ctx) => new(ctx);

    /// <summary>Seeds a tenant on a plan that grants qr_table_menu, plus a category with one
    /// active + one inactive product, and an empty category. Returns the active product id.</summary>
    private static async Task<int> SeedAsync(AppDbContext ctx, bool grantFeature = true, string status = "active")
    {
        var plan = new Plan { Code = "growth", Name = "Growth", MonthlyPriceEgp = 1999, AnnualPriceEgp = 19990 };
        ctx.Plans.Add(plan);
        if (grantFeature)
            ctx.PlanFeatures.Add(new PlanFeature { PlanCode = "growth", FeatureKey = "qr_table_menu" });

        ctx.Tenants.Add(new Tenant
        {
            Id = TenantId, Name = "Acme Café", Slug = Slug, PlanCode = "growth",
            BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = status,
            CreatedAt = DateTime.UtcNow,
        });

        var drinks = new Category { Name = "Drinks" };
        var empty = new Category { Name = "Empty" }; // no products → should be hidden
        ctx.Categories.AddRange(drinks, empty);
        await ctx.SaveChangesAsync();

        var latte = new Product
        {
            Name = "Latte", Description = "Smooth", Price = 35m, CostPrice = 10m, StockQuantity = 100,
            CategoryId = drinks.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        var retired = new Product
        {
            Name = "Retired", Description = "", Price = 10m, CostPrice = 4m, StockQuantity = 0,
            CategoryId = drinks.Id, IsActive = false, CreatedAt = DateTime.UtcNow, // inactive → hidden
        };
        ctx.Products.AddRange(latte, retired);
        await ctx.SaveChangesAsync();

        // Attach a Size modifier group to the latte.
        var size = new ModifierGroup
        {
            Name = "Size", MinSelect = 1, MaxSelect = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        ctx.ModifierGroups.Add(size);
        await ctx.SaveChangesAsync();
        ctx.Modifiers.AddRange(
            new Modifier { ModifierGroupId = size.Id, Name = "Small", PriceDelta = 0m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Modifier { ModifierGroupId = size.Id, Name = "Large", PriceDelta = 10m, IsActive = true, CreatedAt = DateTime.UtcNow });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup { ProductId = latte.Id, ModifierGroupId = size.Id, SortOrder = 0 });
        await ctx.SaveChangesAsync();

        return latte.Id;
    }

    [Fact]
    public async Task GetMenu_ReturnsActiveProducts_WithModifiers_HidesEmptyCategories()
    {
        using var ctx = TestContextFactory.Create();
        await SeedAsync(ctx);
        var svc = Build(ctx);

        var menu = await svc.GetMenuAsync(Slug, CancellationToken.None);

        Assert.Equal("Acme Café", menu.TenantName);
        Assert.Equal("EGP", menu.Currency);
        var cat = Assert.Single(menu.Categories); // empty category hidden
        Assert.Equal("Drinks", cat.Name);
        var product = Assert.Single(cat.Products);  // inactive product hidden
        Assert.Equal("Latte", product.Name);
        Assert.Equal(35m, product.Price);

        var group = Assert.Single(product.ModifierGroups);
        Assert.Equal("Size", group.Name);
        Assert.Equal(2, group.Modifiers.Count);
        Assert.Contains(group.Modifiers, m => m.Name == "Large" && m.PriceDelta == 10m);
    }

    [Fact]
    public async Task GetMenu_CaseInsensitiveSlug()
    {
        using var ctx = TestContextFactory.Create();
        await SeedAsync(ctx);
        var svc = Build(ctx);

        var menu = await svc.GetMenuAsync("ACME", CancellationToken.None);
        Assert.Equal("Acme Café", menu.TenantName);
    }

    [Fact]
    public async Task GetMenu_UnknownSlug_Throws404()
    {
        using var ctx = TestContextFactory.Create();
        await SeedAsync(ctx);
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.GetMenuAsync("nope", CancellationToken.None));
        Assert.Equal("ERR_MENU_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task GetMenu_FeatureNotGranted_Throws404()
    {
        using var ctx = TestContextFactory.Create();
        await SeedAsync(ctx, grantFeature: false);
        var svc = Build(ctx);

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.GetMenuAsync(Slug, CancellationToken.None));
    }

    [Fact]
    public async Task GetMenu_InactiveTenant_Throws404()
    {
        using var ctx = TestContextFactory.Create();
        await SeedAsync(ctx, status: "suspended");
        var svc = Build(ctx);

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.GetMenuAsync(Slug, CancellationToken.None));
    }
}
