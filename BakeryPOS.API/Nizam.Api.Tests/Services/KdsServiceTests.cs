using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.Hubs;
using Nizam.Api.Services.Kds;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for the KDS state machine + <see cref="KdsService"/> — queue filtering, the
/// fire/cooking/ready/served/void transitions, aging colors, and SignalR broadcast targeting.
/// </summary>
public class KdsServiceTests
{
    private const int TenantId = 1;
    private const int BranchId = 7;
    private const int StationId = 3;

    // ----- State machine (pure) -----------------------------------------------------

    [Theory]
    [InlineData(OrderItemStatus.Pending, OrderItemStatus.Fired, true)]
    [InlineData(OrderItemStatus.Fired, OrderItemStatus.Cooking, true)]
    [InlineData(OrderItemStatus.Fired, OrderItemStatus.Ready, true)]
    [InlineData(OrderItemStatus.Fired, OrderItemStatus.Served, true)]
    [InlineData(OrderItemStatus.Cooking, OrderItemStatus.Ready, true)]
    [InlineData(OrderItemStatus.Ready, OrderItemStatus.Served, true)]
    [InlineData(OrderItemStatus.Ready, OrderItemStatus.Voided, true)]
    [InlineData(OrderItemStatus.Pending, OrderItemStatus.Served, false)]   // can't skip firing
    [InlineData(OrderItemStatus.Served, OrderItemStatus.Ready, false)]     // terminal
    [InlineData(OrderItemStatus.Ready, OrderItemStatus.Fired, false)]      // no going back
    [InlineData(OrderItemStatus.Voided, OrderItemStatus.Fired, false)]     // terminal
    public void StateMachine_Transitions(OrderItemStatus from, OrderItemStatus to, bool allowed)
        => Assert.Equal(allowed, KdsItemStateMachine.CanTransition(from, to));

    [Fact]
    public void StateMachine_SameState_IsNoOp()
        => Assert.True(KdsItemStateMachine.CanTransition(OrderItemStatus.Fired, OrderItemStatus.Fired));

    // ----- Service ------------------------------------------------------------------

    private static KdsService Build(AppDbContext ctx, FakeKdsHubContext hub)
        => new(ctx, new AmbientTenant(TenantId), hub, new NoOpAuditService());

    /// <summary>Seeds an order at (branch, table) and an order item at the station with the
    /// given status / fired-at. Returns the order item id.</summary>
    private static async Task<int> SeedItemAsync(
        AppDbContext ctx, OrderItemStatus status, DateTime? firedAt = null,
        int branchId = BranchId, int? stationId = StationId, string product = "Burger")
    {
        var cat = new Category { Name = "Food" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();
        var prod = new Product
        {
            Name = product, Description = "", Price = 50m, CostPrice = 20m, StockQuantity = 100,
            CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(prod);

        var order = new Order
        {
            CashierUserId = 1, BranchId = branchId, Status = OrderStatus.Open,
            Channel = OrderChannel.DineIn, TableId = 42, OpenedAt = DateTime.UtcNow,
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var item = new OrderItem
        {
            OrderId = order.Id, ProductId = prod.Id, Quantity = 1, UnitPrice = 50m, LineTotal = 50m,
            KitchenStationId = stationId, Status = status, FiredAt = firedAt,
        };
        ctx.OrderItems.Add(item);
        await ctx.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task Fire_SetsFiredAt_AndBroadcastsToStationGroup()
    {
        using var ctx = TestContextFactory.Create();
        var hub = new FakeKdsHubContext();
        var svc = Build(ctx, hub);
        var itemId = await SeedItemAsync(ctx, OrderItemStatus.Pending);

        var dto = await svc.FireAsync(itemId, CancellationToken.None);

        Assert.Equal("Fired", dto.Status);
        var item = await ctx.OrderItems.FindAsync(itemId);
        Assert.Equal(OrderItemStatus.Fired, item!.Status);
        Assert.NotNull(item.FiredAt);

        // Broadcast went to the right tenant/branch/station group with the right event.
        Assert.Equal(KdsHub.StationGroup(TenantId, BranchId, StationId), hub.LastGroup);
        Assert.Equal(KdsEvents.ItemFired, hub.LastMethod);
    }

    [Fact]
    public async Task Ready_Then_Served_SetsServedAt()
    {
        using var ctx = TestContextFactory.Create();
        var hub = new FakeKdsHubContext();
        var svc = Build(ctx, hub);
        var itemId = await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow);

        await svc.MarkReadyAsync(itemId, CancellationToken.None);
        Assert.Equal(KdsEvents.ItemReady, hub.LastMethod);

        var dto = await svc.MarkServedAsync(itemId, CancellationToken.None);
        Assert.Equal("Served", dto.Status);
        var item = await ctx.OrderItems.FindAsync(itemId);
        Assert.NotNull(item!.ServedAt);
    }

    [Fact]
    public async Task InvalidTransition_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx, new FakeKdsHubContext());
        var itemId = await SeedItemAsync(ctx, OrderItemStatus.Pending);

        // Pending → Served is not allowed (must fire first).
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.MarkServedAsync(itemId, CancellationToken.None));
        Assert.Equal("ERR_INVALID_KDS_TRANSITION", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task Void_RequiresReason_SetsVoided()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx, new FakeKdsHubContext());
        var itemId = await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow);

        var dto = await svc.VoidAsync(itemId, "86'd — out of buns", CancellationToken.None);
        Assert.Equal("Voided", dto.Status);
        var item = await ctx.OrderItems.FindAsync(itemId);
        Assert.Equal(OrderItemStatus.Voided, item!.Status);
    }

    [Fact]
    public async Task Queue_ReturnsOnlyOpenItems_ForStationAndBranch()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx, new FakeKdsHubContext());

        var fired = await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow);
        await SeedItemAsync(ctx, OrderItemStatus.Ready, firedAt: DateTime.UtcNow);
        await SeedItemAsync(ctx, OrderItemStatus.Served, firedAt: DateTime.UtcNow);          // excluded (done)
        await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow, stationId: 99); // other station
        await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow, branchId: 999); // other branch
        await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow, stationId: null); // unrouted

        var queue = await svc.GetQueueAsync(BranchId, StationId, CancellationToken.None);

        Assert.Equal(2, queue.Count); // only the Fired + Ready at (BranchId, StationId)
        Assert.Contains(queue, q => q.OrderItemId == fired);
        Assert.All(queue, q => Assert.Equal(StationId, q.KitchenStationId));
    }

    [Theory]
    [InlineData(1, "green")]
    [InlineData(6, "yellow")]
    [InlineData(11, "red")]
    public async Task Queue_AgingColor(int minutesAgo, string expectedColor)
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx, new FakeKdsHubContext());
        await SeedItemAsync(ctx, OrderItemStatus.Fired, firedAt: DateTime.UtcNow.AddMinutes(-minutesAgo));

        var queue = await svc.GetQueueAsync(BranchId, StationId, CancellationToken.None);
        Assert.Single(queue);
        Assert.Equal(expectedColor, queue[0].AgeColor);
    }

    // ----- Capturing fake IHubContext<KdsHub> ---------------------------------------

    private sealed class CapturingHubContext : IHubContext<KdsHub>
    {
        private readonly CapturingClients _clients = new();
        public IHubClients Clients => _clients;
        public IGroupManager Groups { get; } = new NoOpGroupManager();
        public string? LastGroup => _clients.LastGroup;
        public string? LastMethod => _clients.LastProxy?.LastMethod;

        private sealed class CapturingClients : IHubClients
        {
            public string? LastGroup;
            public CapturingProxy? LastProxy;

            public IClientProxy Group(string groupName)
            {
                LastGroup = groupName;
                return LastProxy = new CapturingProxy();
            }

            // Unused members — return throwaway proxies.
            public IClientProxy All => new CapturingProxy();
            public IClientProxy AllExcept(IReadOnlyList<string> e) => new CapturingProxy();
            public IClientProxy Client(string c) => new CapturingProxy();
            public IClientProxy Clients(IReadOnlyList<string> c) => new CapturingProxy();
            public IClientProxy GroupExcept(string g, IReadOnlyList<string> e) => new CapturingProxy();
            public IClientProxy Groups(IReadOnlyList<string> g) => new CapturingProxy();
            public IClientProxy User(string u) => new CapturingProxy();
            public IClientProxy Users(IReadOnlyList<string> u) => new CapturingProxy();
        }

        private sealed class CapturingProxy : IClientProxy
        {
            public string? LastMethod;
            public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
            {
                LastMethod = method;
                return Task.CompletedTask;
            }
        }

        private sealed class NoOpGroupManager : IGroupManager
        {
            public Task AddToGroupAsync(string c, string g, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string c, string g, CancellationToken ct = default) => Task.CompletedTask;
        }
    }
}
