using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Mappers;
using Nizam.Api.Services;
using Nizam.Api.Services.Kds;
using Nizam.Api.Services.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ReservationService"/> — CRUD, status transitions, and seating
/// (which delegates to the dine-in flow).
/// </summary>
public class ReservationServiceTests
{
    private const string ActingUser = "host1";

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static ReservationService Build(AppDbContext ctx)
    {
        var kds = new KdsService(ctx, new AmbientTenant(1), new FakeKdsHubContext(), new NoOpAuditService());
        var dineIn = new DineInService(ctx, BuildMapper(), new ModifierApplicationService(ctx), kds, new OrderStateMachine());
        return new ReservationService(ctx, dineIn);
    }

    private static async Task<(int BranchId, int TableId)> SeedBranchTableUserAsync(AppDbContext ctx)
    {
        ctx.Users.Add(new User
        {
            Username = ActingUser, PasswordHash = "n/a", FullName = "Host", IsActive = true,
            Permissions = UserPermissions.ProcessSales,
        });
        var branch = new Branch { Name = "Main", Timezone = "Africa/Cairo", IsActive = true };
        ctx.Branches.Add(branch);
        await ctx.SaveChangesAsync();
        var area = new Area { BranchId = branch.Id, Name = "Indoor", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ctx.Areas.Add(area);
        await ctx.SaveChangesAsync();
        var table = new Table
        {
            BranchId = branch.Id, AreaId = area.Id, Name = "T-1", Capacity = 4,
            Status = TableStatus.Free, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        ctx.Tables.Add(table);
        await ctx.SaveChangesAsync();
        return (branch.Id, table.Id);
    }

    private static ReservationForCreateDto NewDto(int branchId, int? tableId = null) => new()
    {
        BranchId = branchId,
        CustomerName = "Karim",
        Phone = "01000000000",
        PartySize = 4,
        ReservedFor = DateTime.UtcNow.AddHours(1),
        TableId = tableId,
    };

    [Fact]
    public async Task Create_Persists_AsBooked()
    {
        using var ctx = TestContextFactory.Create();
        var (branchId, _) = await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);

        var created = await svc.CreateAsync(NewDto(branchId), CancellationToken.None);
        Assert.Equal("Booked", created.Status);
        Assert.Equal("Karim", created.CustomerName);
    }

    [Fact]
    public async Task Create_NonexistentBranch_Throws()
    {
        using var ctx = TestContextFactory.Create();
        await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.CreateAsync(NewDto(branchId: 9999), CancellationToken.None));
    }

    [Fact]
    public async Task Confirm_FromBooked_Succeeds_AndOnlyFromBooked()
    {
        using var ctx = TestContextFactory.Create();
        var (branchId, _) = await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);
        var r = await svc.CreateAsync(NewDto(branchId), CancellationToken.None);

        var confirmed = await svc.ConfirmAsync(r.Id, CancellationToken.None);
        Assert.Equal("Confirmed", confirmed.Status);

        // Confirming again (already Confirmed, not Booked) is rejected.
        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.ConfirmAsync(r.Id, CancellationToken.None));
        Assert.Equal("ERR_RESERVATION_NOT_BOOKED", ex.ErrorCode);
    }

    [Fact]
    public async Task Cancel_ThenCannotTransition()
    {
        using var ctx = TestContextFactory.Create();
        var (branchId, _) = await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);
        var r = await svc.CreateAsync(NewDto(branchId), CancellationToken.None);

        await svc.CancelAsync(r.Id, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.ConfirmAsync(r.Id, CancellationToken.None));
        Assert.Equal("ERR_RESERVATION_FINALIZED", ex.ErrorCode);
    }

    [Fact]
    public async Task Seat_OpensSession_MarksSeated()
    {
        using var ctx = TestContextFactory.Create();
        var (branchId, tableId) = await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);
        var r = await svc.CreateAsync(NewDto(branchId, tableId), CancellationToken.None);

        var session = await svc.SeatAsync(r.Id, new SeatReservationDto(), ActingUser, CancellationToken.None);

        Assert.Equal(tableId, session.TableId);
        Assert.Equal(4, session.GuestCount); // party size carried over
        var refreshed = await ctx.Reservations.FindAsync(r.Id);
        Assert.Equal(ReservationStatus.Seated, refreshed!.Status);
        var table = await ctx.Tables.FindAsync(tableId);
        Assert.Equal(TableStatus.Occupied, table!.Status);
    }

    [Fact]
    public async Task Seat_NoTable_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var (branchId, _) = await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);
        var r = await svc.CreateAsync(NewDto(branchId, tableId: null), CancellationToken.None); // no pre-assigned table

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.SeatAsync(r.Id, new SeatReservationDto(), ActingUser, CancellationToken.None));
        Assert.Equal("ERR_TABLE_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task List_FiltersByBranchAndDate()
    {
        using var ctx = TestContextFactory.Create();
        var (branchId, _) = await SeedBranchTableUserAsync(ctx);
        var svc = Build(ctx);

        var today = NewDto(branchId);
        today.ReservedFor = DateTime.UtcNow.Date.AddHours(20);
        await svc.CreateAsync(today, CancellationToken.None);

        var tomorrow = NewDto(branchId);
        tomorrow.ReservedFor = DateTime.UtcNow.Date.AddDays(1).AddHours(20);
        await svc.CreateAsync(tomorrow, CancellationToken.None);

        var todayList = await svc.ListForBranchAsync(branchId, DateTime.UtcNow.Date, CancellationToken.None);
        Assert.Single(todayList);
    }
}
