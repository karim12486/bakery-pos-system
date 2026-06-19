using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Mappers;
using Nizam.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="KitchenStationService"/> — station CRUD, name uniqueness,
/// category routing, and the deactivate-clears-routes behaviour.
/// </summary>
public class KitchenStationServiceTests
{
    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static KitchenStationService Build(AppDbContext ctx) => new(ctx, BuildMapper());

    [Fact]
    public async Task Create_Persists()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var created = await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Hot Kitchen", SortOrder = 0 }, CancellationToken.None);
        Assert.Equal("Hot Kitchen", created.Name);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateName_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Bar" }, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateAsync(new KitchenStationForCreateDto { Name = "Bar" }, CancellationToken.None));
        Assert.Equal("ERR_STATION_NAME_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task List_ExcludesInactive_OrdersBySort()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var hot = await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Hot", SortOrder = 10 }, CancellationToken.None);
        await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Bar", SortOrder = 0 }, CancellationToken.None);
        await svc.DeactivateAsync(hot.Id, CancellationToken.None);

        var visible = await svc.ListAsync(includeInactive: false, CancellationToken.None);
        Assert.Single(visible);
        Assert.Equal("Bar", visible[0].Name);

        var all = await svc.ListAsync(includeInactive: true, CancellationToken.None);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task RouteCategory_AssignsStation()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var cat = new Category { Name = "Hot Drinks" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();
        var station = await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Bar" }, CancellationToken.None);

        await svc.RouteCategoryAsync(cat.Id, new CategoryRouteDto { KitchenStationId = station.Id }, CancellationToken.None);

        var refreshed = await ctx.Categories.FindAsync(cat.Id);
        Assert.Equal(station.Id, refreshed!.KitchenStationId);
    }

    [Fact]
    public async Task RouteCategory_ClearWithNull()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var station = await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Bar" }, CancellationToken.None);
        var cat = new Category { Name = "Hot Drinks", KitchenStationId = station.Id };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();

        await svc.RouteCategoryAsync(cat.Id, new CategoryRouteDto { KitchenStationId = null }, CancellationToken.None);

        var refreshed = await ctx.Categories.FindAsync(cat.Id);
        Assert.Null(refreshed!.KitchenStationId);
    }

    [Fact]
    public async Task RouteCategory_NonexistentStation_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var cat = new Category { Name = "Hot Drinks" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.RouteCategoryAsync(cat.Id, new CategoryRouteDto { KitchenStationId = 9999 }, CancellationToken.None));
    }

    [Fact]
    public async Task Deactivate_ClearsCategoryRoutes()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);

        var station = await svc.CreateAsync(new KitchenStationForCreateDto { Name = "Grill" }, CancellationToken.None);
        var cat = new Category { Name = "Burgers", KitchenStationId = station.Id };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();

        await svc.DeactivateAsync(station.Id, CancellationToken.None);

        var refreshed = await ctx.Categories.FindAsync(cat.Id);
        Assert.Null(refreshed!.KitchenStationId); // route cleared on deactivation
    }
}
