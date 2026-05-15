using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Validators;
using Nizam.Api.Mappers;
using Nizam.Api.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

public class ModifierGroupServiceTests
{
    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static ModifierGroupService Build(AppDbContext ctx) => new(
        ctx,
        BuildMapper(),
        new ModifierGroupForCreateDtoValidator(),
        new ModifierGroupForUpdateDtoValidator());

    private static ModifierGroupForCreateDto NewGroupDto(
        string name = "Size",
        int minSelect = 1,
        int maxSelect = 1,
        IEnumerable<ModifierForCreateDto>? modifiers = null) => new()
    {
        Name = name,
        Description = null,
        MinSelect = minSelect,
        MaxSelect = maxSelect,
        SortOrder = 0,
        Modifiers = (modifiers ?? Array.Empty<ModifierForCreateDto>()).ToList(),
    };

    private static ModifierForCreateDto NewModifierDto(string name, decimal delta = 0m, int sort = 0) => new()
    {
        Name = name,
        PriceDelta = delta,
        SortOrder = sort,
        IsDefault = false,
    };

    // ----- Create -------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_BasicGroup_PersistsWithModifiers()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var dto = NewGroupDto("Size", 1, 1, new[]
        {
            NewModifierDto("Small", 0m),
            NewModifierDto("Medium", 10m),
            NewModifierDto("Large", 18m),
        });

        var created = await svc.CreateAsync(dto, CancellationToken.None);

        Assert.Equal("Size", created.Name);
        Assert.Equal(3, created.Modifiers.Count);
        Assert.Equal(0m, created.Modifiers[0].PriceDelta);
        Assert.Equal(18m, created.Modifiers[2].PriceDelta);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None));
        Assert.Equal("ERR_MODIFIER_GROUP_NAME_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_MaxSelectLessThanMinSelect_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var dto = NewGroupDto("Bad", minSelect: 3, maxSelect: 1);

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.CreateAsync(dto, CancellationToken.None));
    }

    // ----- Read ---------------------------------------------------------------------

    [Fact]
    public async Task ListAsync_ExcludesInactive_ByDefault()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var a = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);
        await svc.CreateAsync(NewGroupDto("Milk"), CancellationToken.None);
        await svc.DeactivateAsync(a.Id, CancellationToken.None);

        var visible = await svc.ListAsync(includeInactive: false, CancellationToken.None);
        Assert.Single(visible);
        Assert.Equal("Milk", visible[0].Name);

        var all = await svc.ListAsync(includeInactive: true, CancellationToken.None);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ListAsync_OrdersBySortOrder()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var last = NewGroupDto("Z-Last"); last.SortOrder = 100;
        var first = NewGroupDto("A-First"); first.SortOrder = 0;
        var middle = NewGroupDto("M-Middle"); middle.SortOrder = 50;
        await svc.CreateAsync(last, CancellationToken.None);
        await svc.CreateAsync(first, CancellationToken.None);
        await svc.CreateAsync(middle, CancellationToken.None);

        var ordered = await svc.ListAsync(includeInactive: false, CancellationToken.None);
        Assert.Equal(new[] { "A-First", "M-Middle", "Z-Last" }, ordered.Select(g => g.Name));
    }

    // ----- Update -------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesName_Persists()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var created = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);

        var updated = await svc.UpdateAsync(created.Id, new ModifierGroupForUpdateDto
        {
            Name = "Drink Size",
            MinSelect = 1,
            MaxSelect = 1,
            SortOrder = 5,
            IsActive = true,
        }, CancellationToken.None);

        Assert.Equal("Drink Size", updated.Name);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.UpdateAsync(999, new ModifierGroupForUpdateDto
            {
                Name = "X", MinSelect = 0, MaxSelect = 1, SortOrder = 0, IsActive = true,
            }, CancellationToken.None));
    }

    // ----- Modifiers within a group ------------------------------------------------

    [Fact]
    public async Task AddModifierAsync_AppendsToGroup()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var group = await svc.CreateAsync(NewGroupDto("Extras", minSelect: 0, maxSelect: 5),
            CancellationToken.None);

        await svc.AddModifierAsync(group.Id, NewModifierDto("Extra shot", 8m), CancellationToken.None);

        var refreshed = await svc.GetAsync(group.Id, CancellationToken.None);
        Assert.NotNull(refreshed);
        Assert.Single(refreshed!.Modifiers);
        Assert.Equal("Extra shot", refreshed.Modifiers[0].Name);
        Assert.Equal(8m, refreshed.Modifiers[0].PriceDelta);
    }

    [Fact]
    public async Task UpdateModifierAsync_ModifierNotInGroup_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var groupA = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);
        var groupB = await svc.CreateAsync(NewGroupDto("Milk"), CancellationToken.None);
        var mod = await svc.AddModifierAsync(groupA.Id, NewModifierDto("Large", 18m), CancellationToken.None);

        // Trying to update mod via groupB (wrong group) — must 404, not silently move it.
        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.UpdateModifierAsync(groupB.Id, mod.Id, new ModifierForUpdateDto
            {
                Name = "Hacked",
                PriceDelta = 0m,
                SortOrder = 0,
                IsDefault = false,
                IsActive = true,
            }, CancellationToken.None));
    }

    // ----- Product attach ----------------------------------------------------------

    [Fact]
    public async Task AttachToProductAsync_LinksGroupToProduct()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var (productId, _) = SeedProduct(ctx);
        var group = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);

        await svc.AttachToProductAsync(productId,
            new ProductModifierGroupAttachDto { ModifierGroupId = group.Id, SortOrder = 1 },
            CancellationToken.None);

        var attached = await svc.ListForProductAsync(productId, CancellationToken.None);
        Assert.Single(attached);
        Assert.Equal(group.Id, attached[0].Id);
    }

    [Fact]
    public async Task AttachToProductAsync_Idempotent_UpdatesSortOrderInstead()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var (productId, _) = SeedProduct(ctx);
        var group = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);

        await svc.AttachToProductAsync(productId,
            new ProductModifierGroupAttachDto { ModifierGroupId = group.Id, SortOrder = 1 },
            CancellationToken.None);

        // Re-attach with a different sort order — should silently update, not throw.
        await svc.AttachToProductAsync(productId,
            new ProductModifierGroupAttachDto { ModifierGroupId = group.Id, SortOrder = 5 },
            CancellationToken.None);

        var link = ctx.ProductModifierGroups.Single();
        Assert.Equal(5, link.SortOrder);
    }

    [Fact]
    public async Task DetachFromProductAsync_RemovesLink()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var (productId, _) = SeedProduct(ctx);
        var group = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);
        await svc.AttachToProductAsync(productId,
            new ProductModifierGroupAttachDto { ModifierGroupId = group.Id },
            CancellationToken.None);

        await svc.DetachFromProductAsync(productId, group.Id, CancellationToken.None);

        Assert.Empty(ctx.ProductModifierGroups);
    }

    [Fact]
    public async Task DetachFromProductAsync_NotAttached_NoOp()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var (productId, _) = SeedProduct(ctx);
        var group = await svc.CreateAsync(NewGroupDto("Size"), CancellationToken.None);

        // Should not throw — idempotent detach.
        await svc.DetachFromProductAsync(productId, group.Id, CancellationToken.None);
    }

    // ----- Helpers -----------------------------------------------------------------

    private static (int productId, int categoryId) SeedProduct(AppDbContext ctx)
    {
        var cat = new Category { Name = "Drinks" };
        ctx.Categories.Add(cat);
        ctx.SaveChanges();
        var product = new Product
        {
            Name = "Latte",
            Description = "",
            Price = 35m,
            CostPrice = 10m,
            StockQuantity = 100,
            CategoryId = cat.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);
        ctx.SaveChanges();
        return (product.Id, cat.Id);
    }
}
