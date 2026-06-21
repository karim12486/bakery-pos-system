using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Mappers;
using Nizam.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DineInService"/> — seat / list / transfer / close / clear.
/// Verifies table-status transitions, order envelope creation, occupancy guards, and the
/// session lifecycle.
/// </summary>
public class DineInServiceTests
{
    private const string ActingUser = "server1";

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static DineInService Build(AppDbContext ctx)
    {
        var kds = new Nizam.Api.Services.Kds.KdsService(
            ctx, new Nizam.Api.Common.Tenancy.AmbientTenant(1), new FakeKdsHubContext(), new NoOpAuditService());
        return new(ctx, BuildMapper(), new ModifierApplicationService(ctx), kds,
            new Nizam.Api.Services.Orders.OrderStateMachine());
    }

    /// <summary>Seeds a branch + area + N tables + the acting user. Returns the context and ids.</summary>
    private static async Task<Seed> SeedAsync(int tableCount = 2)
    {
        var ctx = TestContextFactory.Create();

        var user = new User
        {
            Username = ActingUser, PasswordHash = "n/a", FullName = "Server One",
            IsActive = true, Permissions = UserPermissions.ProcessSales,
        };
        ctx.Users.Add(user);

        var branch = new Branch { Name = "Main", Timezone = "Africa/Cairo", IsActive = true };
        ctx.Branches.Add(branch);
        await ctx.SaveChangesAsync();

        var area = new Area
        {
            BranchId = branch.Id, Name = "Indoor", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        ctx.Areas.Add(area);
        await ctx.SaveChangesAsync();

        var tableIds = new List<int>();
        for (var i = 1; i <= tableCount; i++)
        {
            var t = new Table
            {
                BranchId = branch.Id, AreaId = area.Id, Name = $"T-{i}", Capacity = 4,
                Status = TableStatus.Free, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            ctx.Tables.Add(t);
            await ctx.SaveChangesAsync();
            tableIds.Add(t.Id);
        }

        return new Seed(ctx, branch.Id, area.Id, tableIds, user.Id);
    }

    private sealed record Seed(AppDbContext Ctx, int BranchId, int AreaId, List<int> TableIds, int UserId);

    // ----- Seat ---------------------------------------------------------------------

    [Fact]
    public async Task Seat_FreeTable_OpensSessionAndDineInOrder_TableOccupied()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var t1 = s.TableIds[0];

        var session = await svc.SeatAsync(
            new SeatGuestsDto { TableId = t1, GuestCount = 3 }, ActingUser, CancellationToken.None);

        Assert.Equal(t1, session.TableId);
        Assert.Equal(3, session.GuestCount);
        Assert.NotNull(session.OrderId);
        Assert.Null(session.ClosedAt);
        Assert.Equal(s.UserId, session.ServerUserId); // defaulted to acting user

        // Table flipped to Occupied.
        var table = await s.Ctx.Tables.FindAsync(t1);
        Assert.Equal(TableStatus.Occupied, table!.Status);

        // Order created as Open dine-in with the table stamped.
        var order = await s.Ctx.Orders.FirstAsync(o => o.Id == session.OrderId!.Value);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(OrderChannel.DineIn, order.Channel);
        Assert.Equal(t1, order.TableId);
    }

    [Fact]
    public async Task Seat_OccupiedTable_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var t1 = s.TableIds[0];

        await svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 2 }, ActingUser, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 2 }, ActingUser, CancellationToken.None));
        Assert.Equal("ERR_TABLE_OCCUPIED", ex.ErrorCode);
    }

    [Fact]
    public async Task Seat_NonexistentTable_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.SeatAsync(new SeatGuestsDto { TableId = 9999, GuestCount = 2 }, ActingUser, CancellationToken.None));
    }

    [Fact]
    public async Task Seat_ExplicitServer_NotFound_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);

        var ex = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.SeatAsync(
                new SeatGuestsDto { TableId = s.TableIds[0], GuestCount = 2, ServerUserId = 9999 },
                ActingUser, CancellationToken.None));
        Assert.Equal("ERR_SERVER_NOT_FOUND", ex.ErrorCode);
    }

    // ----- List / Get ---------------------------------------------------------------

    [Fact]
    public async Task ListOpen_ReturnsOnlyOpenSessionsForBranch()
    {
        var s = await SeedAsync(tableCount: 3);
        var svc = Build(s.Ctx);

        await svc.SeatAsync(new SeatGuestsDto { TableId = s.TableIds[0], GuestCount = 2 }, ActingUser, CancellationToken.None);
        var second = await svc.SeatAsync(new SeatGuestsDto { TableId = s.TableIds[1], GuestCount = 4 }, ActingUser, CancellationToken.None);
        await svc.CloseAsync(second.Id, CancellationToken.None); // closed → excluded

        var open = await svc.ListOpenForBranchAsync(s.BranchId, CancellationToken.None);
        Assert.Single(open);
        Assert.Equal(s.TableIds[0], open[0].TableId);
    }

    [Fact]
    public async Task GetOpenForTable_FreeTable_ReturnsNull()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);

        Assert.Null(await svc.GetOpenForTableAsync(s.TableIds[0], CancellationToken.None));
    }

    // ----- Transfer -----------------------------------------------------------------

    [Fact]
    public async Task Transfer_MovesSessionAndOrder_SwapsTableStatuses()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var from = s.TableIds[0];
        var to = s.TableIds[1];

        var session = await svc.SeatAsync(new SeatGuestsDto { TableId = from, GuestCount = 2 }, ActingUser, CancellationToken.None);
        var orderId = session.OrderId!.Value;

        var moved = await svc.TransferAsync(session.Id, new TransferTableDto { ToTableId = to }, CancellationToken.None);

        Assert.Equal(to, moved.TableId);

        var fromTable = await s.Ctx.Tables.FindAsync(from);
        var toTable = await s.Ctx.Tables.FindAsync(to);
        Assert.Equal(TableStatus.Dirty, fromTable!.Status);   // vacated
        Assert.Equal(TableStatus.Occupied, toTable!.Status);  // new home

        // Order's TableId follows the session.
        var order = await s.Ctx.Orders.FindAsync(orderId);
        Assert.Equal(to, order!.TableId);
    }

    [Fact]
    public async Task Transfer_ToOccupiedTable_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);

        var first = await svc.SeatAsync(new SeatGuestsDto { TableId = s.TableIds[0], GuestCount = 2 }, ActingUser, CancellationToken.None);
        await svc.SeatAsync(new SeatGuestsDto { TableId = s.TableIds[1], GuestCount = 2 }, ActingUser, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.TransferAsync(first.Id, new TransferTableDto { ToTableId = s.TableIds[1] }, CancellationToken.None));
        Assert.Equal("ERR_TABLE_OCCUPIED", ex.ErrorCode);
    }

    // ----- Close / Clear ------------------------------------------------------------

    [Fact]
    public async Task Close_ClosesSession_TableGoesDirty()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var t1 = s.TableIds[0];

        var session = await svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 2 }, ActingUser, CancellationToken.None);
        await svc.CloseAsync(session.Id, CancellationToken.None);

        var refreshed = await s.Ctx.TableSessions.FindAsync(session.Id);
        Assert.NotNull(refreshed!.ClosedAt);

        var table = await s.Ctx.Tables.FindAsync(t1);
        Assert.Equal(TableStatus.Dirty, table!.Status);
    }

    [Fact]
    public async Task Clear_DirtyTable_GoesFree()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var t1 = s.TableIds[0];

        var session = await svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 2 }, ActingUser, CancellationToken.None);
        await svc.CloseAsync(session.Id, CancellationToken.None);
        await svc.ClearTableAsync(t1, CancellationToken.None);

        var table = await s.Ctx.Tables.FindAsync(t1);
        Assert.Equal(TableStatus.Free, table!.Status);
    }

    [Fact]
    public async Task Clear_OccupiedTable_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var t1 = s.TableIds[0];

        await svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 2 }, ActingUser, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.ClearTableAsync(t1, CancellationToken.None));
        Assert.Equal("ERR_TABLE_OCCUPIED", ex.ErrorCode);
    }

    [Fact]
    public async Task SeatAfterCloseAndClear_Succeeds()
    {
        // Full cycle: seat → close → clear → seat again on the same table.
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var t1 = s.TableIds[0];

        var first = await svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 2 }, ActingUser, CancellationToken.None);
        await svc.CloseAsync(first.Id, CancellationToken.None);
        await svc.ClearTableAsync(t1, CancellationToken.None);

        var second = await svc.SeatAsync(new SeatGuestsDto { TableId = t1, GuestCount = 5 }, ActingUser, CancellationToken.None);
        Assert.Equal(5, second.GuestCount);
        Assert.NotEqual(first.Id, second.Id);
    }

    // ----- Order items (add / fire) -------------------------------------------------

    /// <summary>Seats a table and seeds a routed product. Returns (orderId, productId, stationId).</summary>
    private static async Task<(int OrderId, int ProductId, int StationId)> SeatWithProductAsync(
        Seed s, DineInService svc)
    {
        var station = new KitchenStation
        {
            Name = "Hot Kitchen", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        s.Ctx.KitchenStations.Add(station);
        await s.Ctx.SaveChangesAsync();

        var cat = new Category { Name = "Food", KitchenStationId = station.Id };
        s.Ctx.Categories.Add(cat);
        await s.Ctx.SaveChangesAsync();
        var product = new Product
        {
            Name = "Burger", Description = "", Price = 50m, CostPrice = 20m, StockQuantity = 100,
            CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        s.Ctx.Products.Add(product);
        await s.Ctx.SaveChangesAsync();

        var session = await svc.SeatAsync(
            new SeatGuestsDto { TableId = s.TableIds[0], GuestCount = 2 }, ActingUser, CancellationToken.None);
        return (session.OrderId!.Value, product.Id, station.Id);
    }

    [Fact]
    public async Task AddItems_CreatesPendingItems_RecomputesTotal_RoutesStation()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var (orderId, productId, stationId) = await SeatWithProductAsync(s, svc);

        var dto = new AddOrderItemsDto
        {
            Items = new List<SaleDetailForCreateDto> { new() { ProductId = productId, Quantity = 2 } }
        };
        var order = await svc.AddItemsAsync(orderId, dto, CancellationToken.None);

        Assert.Single(order.Items);
        Assert.Equal("Pending", order.Items[0].Status);
        Assert.Equal(stationId, order.Items[0].KitchenStationId);
        Assert.Equal(100m, order.Subtotal);     // 2 × 50
        Assert.Equal(100m, order.FinalAmount);
    }

    [Fact]
    public async Task FireOrder_TransitionsPendingToFired_AndStampsFiredAt()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var (orderId, productId, _) = await SeatWithProductAsync(s, svc);

        await svc.AddItemsAsync(orderId,
            new AddOrderItemsDto { Items = new() { new() { ProductId = productId, Quantity = 1 } } },
            CancellationToken.None);

        var fired = await svc.FireOrderAsync(orderId, CancellationToken.None);
        Assert.Equal("Fired", fired.Items[0].Status);

        var item = await s.Ctx.OrderItems.SingleAsync();
        Assert.Equal(OrderItemStatus.Fired, item.Status);
        Assert.NotNull(item.FiredAt);
    }

    [Fact]
    public async Task AddItems_ToNonDineInOrder_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);

        // A takeaway order — not dine-in.
        var order = new Order
        {
            CashierUserId = s.UserId, BranchId = s.BranchId, Status = OrderStatus.Open,
            Channel = OrderChannel.Takeaway, OpenedAt = DateTime.UtcNow,
        };
        s.Ctx.Orders.Add(order);
        await s.Ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.AddItemsAsync(order.Id,
                new AddOrderItemsDto { Items = new() { new() { ProductId = 1, Quantity = 1 } } },
                CancellationToken.None));
        Assert.Equal("ERR_NOT_DINE_IN", ex.ErrorCode);
    }

    // ----- Merge --------------------------------------------------------------------

    [Fact]
    public async Task Merge_MovesItems_SumsGuests_FreesSourceTable_CancelsSourceOrder()
    {
        var s = await SeedAsync(tableCount: 2);
        var svc = Build(s.Ctx);
        var (destOrderId, productId, _) = await SeatWithProductAsync(s, svc);
        var destSession = await svc.GetOpenForTableAsync(s.TableIds[0], CancellationToken.None);

        // Source: seat table 2 (3 guests) + add an item.
        var sourceSession = await svc.SeatAsync(
            new SeatGuestsDto { TableId = s.TableIds[1], GuestCount = 3 }, ActingUser, CancellationToken.None);
        await svc.AddItemsAsync(sourceSession.OrderId!.Value,
            new AddOrderItemsDto { Items = new() { new() { ProductId = productId, Quantity = 1 } } },
            CancellationToken.None);
        // Dest gets an item too.
        await svc.AddItemsAsync(destOrderId,
            new AddOrderItemsDto { Items = new() { new() { ProductId = productId, Quantity = 1 } } },
            CancellationToken.None);

        var merged = await svc.MergeAsync(sourceSession.Id, destSession!.Id, CancellationToken.None);

        // Destination session keeps going, guests summed (2 + 3 = 5).
        Assert.Equal(destSession.Id, merged.Id);
        Assert.Equal(5, merged.GuestCount);

        // Both items now on the destination order.
        Assert.Equal(2, await s.Ctx.OrderItems.CountAsync(oi => oi.OrderId == destOrderId));

        // Source order cancelled, source session closed, source table Dirty.
        var sourceOrder = await s.Ctx.Orders.FindAsync(sourceSession.OrderId!.Value);
        Assert.Equal(OrderStatus.Cancelled, sourceOrder!.Status);
        var sourceSess = await s.Ctx.TableSessions.FindAsync(sourceSession.Id);
        Assert.NotNull(sourceSess!.ClosedAt);
        var sourceTable = await s.Ctx.Tables.FindAsync(s.TableIds[1]);
        Assert.Equal(TableStatus.Dirty, sourceTable!.Status);
    }

    [Fact]
    public async Task Merge_IntoItself_Throws()
    {
        var s = await SeedAsync();
        var svc = Build(s.Ctx);
        var (_, _, _) = await SeatWithProductAsync(s, svc);
        var session = await svc.GetOpenForTableAsync(s.TableIds[0], CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.MergeAsync(session!.Id, session.Id, CancellationToken.None));
        Assert.Equal("ERR_MERGE_SAME_SESSION", ex.ErrorCode);
    }
}
