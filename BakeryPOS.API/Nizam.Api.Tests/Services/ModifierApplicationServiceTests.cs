using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.Services;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ModifierApplicationService"/>: validates per-group
/// min/max rules, attaches snapshot rows, and computes the per-unit price delta.
/// </summary>
public class ModifierApplicationServiceTests
{
    /// <summary>Builds a Latte product attached to:
    ///   - Size group (required, pick 1: Small +0, Medium +10, Large +18)
    ///   - Milk group (required, pick 1: Whole +0, Oat +12)
    ///   - Extras group (optional, pick any: Extra shot +8, Vanilla +5)
    /// Returns (ctx, productId, modifierIds[Small, Medium, Large, Whole, Oat, ExtraShot, Vanilla]).
    /// </summary>
    private static async Task<TestSetup> BuildLatteCatalogAsync()
    {
        var ctx = TestContextFactory.Create();

        var cat = new Category { Name = "Drinks" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();

        var product = new Product
        {
            Name = "Latte", Description = "", Price = 35m, CostPrice = 10m,
            StockQuantity = 100, CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var sizeGroup = new ModifierGroup
        {
            Name = "Size", MinSelect = 1, MaxSelect = 1, SortOrder = 0, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var milkGroup = new ModifierGroup
        {
            Name = "Milk", MinSelect = 1, MaxSelect = 1, SortOrder = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var extrasGroup = new ModifierGroup
        {
            Name = "Extras", MinSelect = 0, MaxSelect = 5, SortOrder = 2, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        ctx.ModifierGroups.AddRange(sizeGroup, milkGroup, extrasGroup);
        await ctx.SaveChangesAsync();

        var small = new Modifier { ModifierGroupId = sizeGroup.Id, Name = "Small", PriceDelta = 0, IsActive = true, CreatedAt = DateTime.UtcNow };
        var medium = new Modifier { ModifierGroupId = sizeGroup.Id, Name = "Medium", PriceDelta = 10m, IsActive = true, CreatedAt = DateTime.UtcNow };
        var large = new Modifier { ModifierGroupId = sizeGroup.Id, Name = "Large", PriceDelta = 18m, IsActive = true, CreatedAt = DateTime.UtcNow };
        var whole = new Modifier { ModifierGroupId = milkGroup.Id, Name = "Whole", PriceDelta = 0, IsActive = true, CreatedAt = DateTime.UtcNow };
        var oat = new Modifier { ModifierGroupId = milkGroup.Id, Name = "Oat", PriceDelta = 12m, IsActive = true, CreatedAt = DateTime.UtcNow };
        var extraShot = new Modifier { ModifierGroupId = extrasGroup.Id, Name = "Extra shot", PriceDelta = 8m, IsActive = true, CreatedAt = DateTime.UtcNow };
        var vanilla = new Modifier { ModifierGroupId = extrasGroup.Id, Name = "Vanilla", PriceDelta = 5m, IsActive = true, CreatedAt = DateTime.UtcNow };
        ctx.Modifiers.AddRange(small, medium, large, whole, oat, extraShot, vanilla);
        await ctx.SaveChangesAsync();

        ctx.ProductModifierGroups.AddRange(
            new ProductModifierGroup { ProductId = product.Id, ModifierGroupId = sizeGroup.Id, SortOrder = 0 },
            new ProductModifierGroup { ProductId = product.Id, ModifierGroupId = milkGroup.Id, SortOrder = 1 },
            new ProductModifierGroup { ProductId = product.Id, ModifierGroupId = extrasGroup.Id, SortOrder = 2 });
        await ctx.SaveChangesAsync();

        return new TestSetup(ctx, product.Id, sizeGroup.Id, milkGroup.Id, extrasGroup.Id,
            small.Id, medium.Id, large.Id, whole.Id, oat.Id, extraShot.Id, vanilla.Id);
    }

    private sealed record TestSetup(
        AppDbContext Ctx, int ProductId,
        int SizeGroupId, int MilkGroupId, int ExtrasGroupId,
        int Small, int Medium, int Large, int Whole, int Oat, int ExtraShot, int Vanilla);

    // ----- Happy path ---------------------------------------------------------------

    [Fact]
    public async Task PrepareAsync_AllRequiredGroupsSatisfied_ReturnsCorrectDelta()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Large + Oat + ExtraShot = 18 + 12 + 8 = 38
        var result = await svc.PrepareAsync(s.ProductId,
            new[] { s.Large, s.Oat, s.ExtraShot }, CancellationToken.None);

        Assert.Equal(38m, result.TotalPriceDelta);
        Assert.Equal(3, result.Snapshots.Count);
    }

    [Fact]
    public async Task PrepareAsync_SnapshotsCarryGroupAndModifierNames()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        var result = await svc.PrepareAsync(s.ProductId,
            new[] { s.Large, s.Oat }, CancellationToken.None);

        var size = result.Snapshots.Single(x => x.GroupName == "Size");
        Assert.Equal("Large", size.Name);
        Assert.Equal(18m, size.PriceDelta);

        var milk = result.Snapshots.Single(x => x.GroupName == "Milk");
        Assert.Equal("Oat", milk.Name);
        Assert.Equal(12m, milk.PriceDelta);
    }

    [Fact]
    public async Task PrepareAsync_OptionalGroupOmitted_Succeeds()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Size + Milk satisfied, Extras (optional) skipped.
        var result = await svc.PrepareAsync(s.ProductId,
            new[] { s.Small, s.Whole }, CancellationToken.None);

        Assert.Equal(0m, result.TotalPriceDelta);
        Assert.Equal(2, result.Snapshots.Count);
    }

    [Fact]
    public async Task PrepareAsync_MultiSelectGroup_MultipleExtras_AllSnapshotted()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Extras can have 0..5 picks: pick both Extra shot + Vanilla = 8 + 5 = 13 delta.
        var result = await svc.PrepareAsync(s.ProductId,
            new[] { s.Small, s.Whole, s.ExtraShot, s.Vanilla }, CancellationToken.None);

        Assert.Equal(13m, result.TotalPriceDelta);
        Assert.Equal(2, result.Snapshots.Count(x => x.GroupName == "Extras"));
    }

    // ----- Validation ---------------------------------------------------------------

    [Fact]
    public async Task PrepareAsync_RequiredGroupMissing_Throws()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Only Milk picked; Size (required min=1) missing.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PrepareAsync(s.ProductId, new[] { s.Whole }, CancellationToken.None));

        Assert.Equal("ERR_MODIFIER_MIN_NOT_MET", ex.ErrorCode);
        Assert.Contains("Size", ex.Message);
    }

    [Fact]
    public async Task PrepareAsync_SingleChoiceGroup_PickedTwice_Throws()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Picking 2 sizes — MaxSelect=1.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PrepareAsync(s.ProductId,
                new[] { s.Small, s.Large, s.Whole }, CancellationToken.None));

        Assert.Equal("ERR_MODIFIER_MAX_EXCEEDED", ex.ErrorCode);
        Assert.Contains("Size", ex.Message);
    }

    [Fact]
    public async Task PrepareAsync_ModifierFromUnattachedGroup_Throws()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Create a new group NOT attached to this product.
        var foreignGroup = new ModifierGroup
        {
            Name = "Syrup", MinSelect = 0, MaxSelect = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        s.Ctx.ModifierGroups.Add(foreignGroup);
        await s.Ctx.SaveChangesAsync();

        var foreignMod = new Modifier
        {
            ModifierGroupId = foreignGroup.Id, Name = "Caramel", PriceDelta = 5m,
            IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        s.Ctx.Modifiers.Add(foreignMod);
        await s.Ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PrepareAsync(s.ProductId,
                new[] { s.Small, s.Whole, foreignMod.Id }, CancellationToken.None));

        Assert.Equal("ERR_MODIFIER_NOT_AVAILABLE", ex.ErrorCode);
    }

    [Fact]
    public async Task PrepareAsync_InactiveModifier_Rejected()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Deactivate Large.
        var large = await s.Ctx.Modifiers.FindAsync(s.Large);
        large!.IsActive = false;
        await s.Ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PrepareAsync(s.ProductId,
                new[] { s.Large, s.Whole }, CancellationToken.None));

        Assert.Equal("ERR_MODIFIER_NOT_AVAILABLE", ex.ErrorCode);
    }

    [Fact]
    public async Task PrepareAsync_InactiveGroup_ItsModifiersRejected()
    {
        var s = await BuildLatteCatalogAsync();
        var svc = new ModifierApplicationService(s.Ctx);

        // Deactivate the entire Size group; its picks become unavailable.
        var sizeGroup = await s.Ctx.ModifierGroups.FindAsync(s.SizeGroupId);
        sizeGroup!.IsActive = false;
        await s.Ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PrepareAsync(s.ProductId,
                new[] { s.Small, s.Whole }, CancellationToken.None));

        Assert.Equal("ERR_MODIFIER_NOT_AVAILABLE", ex.ErrorCode);
    }

    [Fact]
    public async Task PrepareAsync_ProductWithNoAttachedGroups_EmptySelectionOk()
    {
        using var ctx = TestContextFactory.Create();
        var cat = new Category { Name = "X" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();
        var product = new Product
        {
            Name = "Cookie", Description = "", Price = 5m, CostPrice = 1m, StockQuantity = 10,
            CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var svc = new ModifierApplicationService(ctx);
        var result = await svc.PrepareAsync(product.Id, Array.Empty<int>(), CancellationToken.None);

        Assert.Equal(0m, result.TotalPriceDelta);
        Assert.Empty(result.Snapshots);
    }
}
