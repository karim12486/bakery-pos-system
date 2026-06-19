using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Mappers;
using Nizam.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AreaService"/> + <see cref="TableService"/> — branch-scoped
/// floor-plan CRUD, name uniqueness, cross-branch attachment guards, and the single-payload
/// floor-plan assembly.
/// </summary>
public class FloorPlanServiceTests
{
    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static AreaService BuildAreaService(AppDbContext ctx) => new(ctx, BuildMapper());
    private static TableService BuildTableService(AppDbContext ctx) => new(ctx, BuildMapper());

    /// <summary>Seeds two branches under the default tenant. Returns their ids.</summary>
    private static async Task<(AppDbContext Ctx, int Branch1, int Branch2)> SeedBranchesAsync()
    {
        var ctx = TestContextFactory.Create();
        var b1 = new Branch { Name = "Main", Timezone = "Africa/Cairo", IsActive = true };
        var b2 = new Branch { Name = "Mall", Timezone = "Africa/Cairo", IsActive = true };
        ctx.Branches.AddRange(b1, b2);
        await ctx.SaveChangesAsync();
        return (ctx, b1.Id, b2.Id);
    }

    // ----- Area CRUD ----------------------------------------------------------------

    [Fact]
    public async Task CreateArea_Persists()
    {
        var (ctx, branch1, _) = await SeedBranchesAsync();
        var svc = BuildAreaService(ctx);

        var created = await svc.CreateAsync(
            new AreaForCreateDto { BranchId = branch1, Name = "Indoor", SortOrder = 0 },
            CancellationToken.None);

        Assert.Equal("Indoor", created.Name);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task CreateArea_DuplicateNameInSameBranch_Throws()
    {
        var (ctx, branch1, _) = await SeedBranchesAsync();
        var svc = BuildAreaService(ctx);

        await svc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None));
        Assert.Equal("ERR_AREA_NAME_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateArea_SameNameDifferentBranch_Allowed()
    {
        var (ctx, branch1, branch2) = await SeedBranchesAsync();
        var svc = BuildAreaService(ctx);

        await svc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);
        // Same name, different branch — should succeed (uniqueness is per-branch).
        var second = await svc.CreateAsync(
            new AreaForCreateDto { BranchId = branch2, Name = "Indoor" }, CancellationToken.None);
        Assert.Equal("Indoor", second.Name);
    }

    [Fact]
    public async Task CreateArea_NonexistentBranch_Throws()
    {
        var (ctx, _, _) = await SeedBranchesAsync();
        var svc = BuildAreaService(ctx);

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.CreateAsync(new AreaForCreateDto { BranchId = 9999, Name = "Indoor" }, CancellationToken.None));
    }

    [Fact]
    public async Task ListAreasForBranch_ExcludesInactiveByDefault_AndOtherBranches()
    {
        var (ctx, branch1, branch2) = await SeedBranchesAsync();
        var svc = BuildAreaService(ctx);

        var a = await svc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor", SortOrder = 0 }, CancellationToken.None);
        await svc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Outdoor", SortOrder = 1 }, CancellationToken.None);
        await svc.CreateAsync(new AreaForCreateDto { BranchId = branch2, Name = "Bar", SortOrder = 0 }, CancellationToken.None);
        await svc.DeactivateAsync(a.Id, CancellationToken.None);

        var visible = await svc.ListForBranchAsync(branch1, includeInactive: false, CancellationToken.None);
        Assert.Single(visible);
        Assert.Equal("Outdoor", visible[0].Name);

        var all = await svc.ListForBranchAsync(branch1, includeInactive: true, CancellationToken.None);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task DeactivateArea_WithActiveTables_Throws()
    {
        var (ctx, branch1, _) = await SeedBranchesAsync();
        var areaSvc = BuildAreaService(ctx);
        var tableSvc = BuildTableService(ctx);

        var area = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);
        await tableSvc.CreateAsync(
            new TableForCreateDto { BranchId = branch1, AreaId = area.Id, Name = "T1", Capacity = 4 },
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => areaSvc.DeactivateAsync(area.Id, CancellationToken.None));
        Assert.Equal("ERR_AREA_HAS_TABLES", ex.ErrorCode);
    }

    // ----- Table CRUD ---------------------------------------------------------------

    [Fact]
    public async Task CreateTable_Persists_WithFreeStatus()
    {
        var (ctx, branch1, _) = await SeedBranchesAsync();
        var areaSvc = BuildAreaService(ctx);
        var tableSvc = BuildTableService(ctx);

        var area = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);
        var table = await tableSvc.CreateAsync(new TableForCreateDto
        {
            BranchId = branch1, AreaId = area.Id, Name = "T-12", Capacity = 4,
            Shape = TableShape.Round, X = 100, Y = 200, Width = 80, Height = 80,
        }, CancellationToken.None);

        Assert.Equal("T-12", table.Name);
        Assert.Equal(TableStatus.Free, table.Status);
        Assert.Equal(TableShape.Round, table.Shape);
        Assert.Equal(100, table.X);
    }

    [Fact]
    public async Task CreateTable_AreaFromDifferentBranch_Throws()
    {
        var (ctx, branch1, branch2) = await SeedBranchesAsync();
        var areaSvc = BuildAreaService(ctx);
        var tableSvc = BuildTableService(ctx);

        // Area belongs to branch1.
        var area = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);

        // Try to create a table on branch2 referencing branch1's area.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => tableSvc.CreateAsync(new TableForCreateDto
            {
                BranchId = branch2, AreaId = area.Id, Name = "T1", Capacity = 2,
            }, CancellationToken.None));
        Assert.Equal("ERR_AREA_BRANCH_MISMATCH", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateTable_DuplicateNameInBranch_Throws()
    {
        var (ctx, branch1, _) = await SeedBranchesAsync();
        var areaSvc = BuildAreaService(ctx);
        var tableSvc = BuildTableService(ctx);

        var area = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);
        await tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch1, AreaId = area.Id, Name = "T1", Capacity = 2 }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch1, AreaId = area.Id, Name = "T1", Capacity = 2 }, CancellationToken.None));
        Assert.Equal("ERR_TABLE_NAME_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task DeactivateTable_WhenOccupied_Throws()
    {
        var (ctx, branch1, _) = await SeedBranchesAsync();
        var areaSvc = BuildAreaService(ctx);
        var tableSvc = BuildTableService(ctx);

        var area = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor" }, CancellationToken.None);
        var table = await tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch1, AreaId = area.Id, Name = "T1", Capacity = 2 }, CancellationToken.None);

        // Simulate occupancy directly (TableSession entity ships next branch).
        var entity = await ctx.Tables.FindAsync(table.Id);
        entity!.Status = TableStatus.Occupied;
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => tableSvc.DeactivateAsync(table.Id, CancellationToken.None));
        Assert.Equal("ERR_TABLE_OCCUPIED", ex.ErrorCode);
    }

    // ----- Floor plan assembly ------------------------------------------------------

    [Fact]
    public async Task GetFloorPlan_GroupsTablesUnderAreas()
    {
        var (ctx, branch1, branch2) = await SeedBranchesAsync();
        var areaSvc = BuildAreaService(ctx);
        var tableSvc = BuildTableService(ctx);

        var indoor = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Indoor", SortOrder = 0 }, CancellationToken.None);
        var outdoor = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch1, Name = "Outdoor", SortOrder = 1 }, CancellationToken.None);

        await tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch1, AreaId = indoor.Id, Name = "I-1", Capacity = 2 }, CancellationToken.None);
        await tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch1, AreaId = indoor.Id, Name = "I-2", Capacity = 4 }, CancellationToken.None);
        await tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch1, AreaId = outdoor.Id, Name = "O-1", Capacity = 6 }, CancellationToken.None);

        // Noise on another branch — must not appear.
        var otherArea = await areaSvc.CreateAsync(new AreaForCreateDto { BranchId = branch2, Name = "Bar" }, CancellationToken.None);
        await tableSvc.CreateAsync(new TableForCreateDto { BranchId = branch2, AreaId = otherArea.Id, Name = "B-1", Capacity = 2 }, CancellationToken.None);

        var plan = await tableSvc.GetFloorPlanAsync(branch1, CancellationToken.None);

        Assert.Equal(branch1, plan.BranchId);
        Assert.Equal(2, plan.Areas.Count);
        Assert.Equal("Indoor", plan.Areas[0].Name);  // sort order 0
        Assert.Equal(2, plan.Areas[0].Tables.Count);
        Assert.Equal("Outdoor", plan.Areas[1].Name);
        Assert.Single(plan.Areas[1].Tables);
    }
}
