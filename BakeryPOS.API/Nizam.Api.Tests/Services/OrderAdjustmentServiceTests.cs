using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Nizam.Api.Services.Kds;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="OrderAdjustmentService"/> — void (with fired-item manager
/// override) and comp (always manager-gated), plus order-total recompute.
/// </summary>
public class OrderAdjustmentServiceTests
{
    private const string ManagerPin = "9999";

    private static readonly IPasswordService Passwords = new PasswordService();

    private static OrderAdjustmentService Build(AppDbContext ctx)
    {
        var pin = new PinAuthService(ctx, Passwords, new StubToken());
        var kds = new KdsService(ctx, new AmbientTenant(1), new FakeKdsHubContext(), new NoOpAuditService());
        return new OrderAdjustmentService(ctx, pin, kds, new NoOpAuditService());
    }

    /// <summary>Seeds a manager (PIN, ApproveRemovals), an order, and one item of the given
    /// status + line total. Returns the order item id.</summary>
    private static async Task<(int OrderId, int ItemId)> SeedAsync(
        AppDbContext ctx, OrderItemStatus status, decimal lineTotal = 50m, bool seedManager = true)
    {
        if (seedManager)
        {
            ctx.Users.Add(new User
            {
                Username = "boss", PasswordHash = "n/a", FullName = "Boss", IsActive = true,
                Permissions = UserPermissions.ApproveRemovals, PinHash = Passwords.HashPassword(ManagerPin),
            });
        }
        var cat = new Category { Name = "Food" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();
        var product = new Product
        {
            Name = "Dish", Description = "", Price = lineTotal, CostPrice = 10m, StockQuantity = 100,
            CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);
        var order = new Order
        {
            CashierUserId = 1, BranchId = 1, Status = OrderStatus.Open, Channel = OrderChannel.DineIn,
            OpenedAt = DateTime.UtcNow, Subtotal = lineTotal, FinalAmount = lineTotal,
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();
        var item = new OrderItem
        {
            OrderId = order.Id, ProductId = product.Id, Quantity = 1, UnitPrice = lineTotal,
            LineTotal = lineTotal, Status = status,
            FiredAt = status == OrderItemStatus.Fired ? DateTime.UtcNow : null,
        };
        ctx.OrderItems.Add(item);
        await ctx.SaveChangesAsync();
        return (order.Id, item.Id);
    }

    // ----- Void --------------------------------------------------------------------

    [Fact]
    public async Task Void_PendingItem_NoOverrideNeeded()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, itemId) = await SeedAsync(ctx, OrderItemStatus.Pending);

        await svc.VoidItemAsync(itemId, new VoidItemDto { Reason = "wrong item" }, CancellationToken.None);

        var item = await ctx.OrderItems.FindAsync(itemId);
        Assert.Equal(OrderItemStatus.Voided, item!.Status);
        var order = await ctx.Orders.FindAsync(orderId);
        Assert.Equal(0m, order!.Subtotal); // voided item drops from total
    }

    [Fact]
    public async Task Void_FiredItem_WithoutManagerPin_Throws403()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (_, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.VoidItemAsync(itemId, new VoidItemDto { Reason = "86" }, CancellationToken.None));
        Assert.Equal("ERR_OVERRIDE_REQUIRED", ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Void_FiredItem_WithValidManagerPin_Succeeds()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (_, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired);

        await svc.VoidItemAsync(itemId,
            new VoidItemDto { Reason = "86", ManagerPin = ManagerPin }, CancellationToken.None);

        var item = await ctx.OrderItems.FindAsync(itemId);
        Assert.Equal(OrderItemStatus.Voided, item!.Status);
    }

    [Fact]
    public async Task Void_FiredItem_WrongManagerPin_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (_, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired);

        await Assert.ThrowsAsync<DomainException>(
            () => svc.VoidItemAsync(itemId,
                new VoidItemDto { Reason = "86", ManagerPin = "0000" }, CancellationToken.None));
    }

    // ----- Comp --------------------------------------------------------------------

    [Fact]
    public async Task Comp_WithManagerPin_ZeroesLineTotal_RecomputesOrder()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired, lineTotal: 80m);

        await svc.CompItemAsync(itemId,
            new CompItemDto { Reason = "birthday", ManagerPin = ManagerPin }, CancellationToken.None);

        var item = await ctx.OrderItems.FindAsync(itemId);
        Assert.True(item!.IsComped);
        Assert.Equal(0m, item.LineTotal);
        Assert.Equal(80m, item.UnitPrice); // original retained for reporting

        var order = await ctx.Orders.FindAsync(orderId);
        Assert.Equal(0m, order!.Subtotal); // comped item contributes 0
    }

    [Fact]
    public async Task Comp_WithoutValidManager_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (_, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired);

        await Assert.ThrowsAsync<DomainException>(
            () => svc.CompItemAsync(itemId,
                new CompItemDto { Reason = "x", ManagerPin = "0000" }, CancellationToken.None));
    }

    [Fact]
    public async Task Comp_NonManagerPin_Throws403()
    {
        using var ctx = TestContextFactory.Create();
        // Seed a cashier with a PIN but NOT ApproveRemovals.
        ctx.Users.Add(new User
        {
            Username = "cashier", PasswordHash = "n/a", FullName = "Cashier", IsActive = true,
            Permissions = UserPermissions.ProcessSales, PinHash = Passwords.HashPassword("1111"),
        });
        await ctx.SaveChangesAsync();
        var svc = Build(ctx);
        var (_, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired, seedManager: false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CompItemAsync(itemId,
                new CompItemDto { Reason = "x", ManagerPin = "1111" }, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode); // has PIN but lacks ApproveRemovals
    }

    [Fact]
    public async Task Comp_AlreadyComped_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (_, itemId) = await SeedAsync(ctx, OrderItemStatus.Fired);
        await svc.CompItemAsync(itemId, new CompItemDto { Reason = "x", ManagerPin = ManagerPin }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CompItemAsync(itemId, new CompItemDto { Reason = "y", ManagerPin = ManagerPin }, CancellationToken.None));
        Assert.Equal("ERR_ALREADY_COMPED", ex.ErrorCode);
    }

    private sealed class StubToken : ITokenService
    {
        public string CreateToken(User user, int? branchId = null) => "t";
        public string CreateSuperAdminToken(SuperAdmin admin) => "s";
    }
}
