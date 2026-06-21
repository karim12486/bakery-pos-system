using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Nizam.Api.Services.Orders;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CheckService"/> — the four split modes, money math (12% service
/// charge + branch tax + opt-out), re-split rules, and pay-closes-order behaviour.
/// </summary>
public class CheckServiceTests
{
    private static CheckService Build(AppDbContext ctx) => new(ctx, new OrderStateMachine());

    /// <summary>Seeds a branch (tax rate), a dine-in order, and N order items of the given price.
    /// Returns (orderId, itemIds).</summary>
    private static async Task<(int OrderId, List<int> ItemIds)> SeedOrderAsync(
        AppDbContext ctx, decimal taxRate, params decimal[] lineTotals)
    {
        var branch = new Branch { Name = "Main", Timezone = "Africa/Cairo", IsActive = true, TaxRate = taxRate };
        ctx.Branches.Add(branch);
        var cat = new Category { Name = "Food" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();
        var product = new Product
        {
            Name = "Dish", Description = "", Price = 10m, CostPrice = 4m, StockQuantity = 100,
            CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);

        var order = new Order
        {
            CashierUserId = 1, BranchId = branch.Id, Status = OrderStatus.Open,
            Channel = OrderChannel.DineIn, OpenedAt = DateTime.UtcNow,
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var ids = new List<int>();
        foreach (var lt in lineTotals)
        {
            var item = new OrderItem
            {
                OrderId = order.Id, ProductId = product.Id, Quantity = 1, UnitPrice = lt, LineTotal = lt,
                Status = OrderItemStatus.Fired,
            };
            ctx.OrderItems.Add(item);
            await ctx.SaveChangesAsync();
            ids.Add(item.Id);
        }
        return (order.Id, ids);
    }

    // ----- Single ------------------------------------------------------------------

    [Fact]
    public async Task Single_OneCheck_AllItems_ServiceChargeAndTax()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, ids) = await SeedOrderAsync(ctx, taxRate: 0.14m, 100m, 50m); // subtotal 150

        var checks = await svc.CreateChecksAsync(orderId, new CreateChecksDto { Mode = SplitMode.Single }, CancellationToken.None);

        var c = Assert.Single(checks);
        Assert.Equal(150m, c.Subtotal);
        Assert.Equal(18m, c.ServiceChargeAmount);   // 150 × 12%
        Assert.Equal(21m, c.TaxAmount);             // 150 × 14%
        Assert.Equal(189m, c.Total);
        Assert.Equal(2, c.OrderItemIds.Count);
    }

    // ----- ByItem ------------------------------------------------------------------

    [Fact]
    public async Task ByItem_AssignsItemsToChecks()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, ids) = await SeedOrderAsync(ctx, taxRate: 0m, 100m, 50m, 30m);

        var dto = new CreateChecksDto
        {
            Mode = SplitMode.ByItem,
            Assignments = new()
            {
                new CheckAssignmentDto { Label = "Alice", OrderItemIds = new() { ids[0] } },
                new CheckAssignmentDto { Label = "Bob", OrderItemIds = new() { ids[1], ids[2] } },
            }
        };
        var checks = await svc.CreateChecksAsync(orderId, dto, CancellationToken.None);

        Assert.Equal(2, checks.Count);
        Assert.Equal(100m, checks[0].Subtotal);
        Assert.Equal(80m, checks[1].Subtotal);
        Assert.Equal(12m, checks[0].ServiceChargeAmount); // 100 × 12%
    }

    [Fact]
    public async Task ByItem_UnassignedItem_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, ids) = await SeedOrderAsync(ctx, taxRate: 0m, 100m, 50m);

        var dto = new CreateChecksDto
        {
            Mode = SplitMode.ByItem,
            Assignments = new() { new CheckAssignmentDto { OrderItemIds = new() { ids[0] } } }, // ids[1] missing
        };
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateChecksAsync(orderId, dto, CancellationToken.None));
        Assert.Equal("ERR_ITEMS_UNASSIGNED", ex.ErrorCode);
    }

    [Fact]
    public async Task ByItem_DuplicateItem_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, ids) = await SeedOrderAsync(ctx, taxRate: 0m, 100m, 50m);

        var dto = new CreateChecksDto
        {
            Mode = SplitMode.ByItem,
            Assignments = new()
            {
                new CheckAssignmentDto { OrderItemIds = new() { ids[0], ids[1] } },
                new CheckAssignmentDto { OrderItemIds = new() { ids[1] } }, // dup
            }
        };
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateChecksAsync(orderId, dto, CancellationToken.None));
        Assert.Equal("ERR_ITEM_DUPLICATED", ex.ErrorCode);
    }

    // ----- Equal -------------------------------------------------------------------

    [Fact]
    public async Task Equal_DividesEvenly_RemainderToFirst()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 100m); // subtotal 100, split 3

        var checks = await svc.CreateChecksAsync(orderId,
            new CreateChecksDto { Mode = SplitMode.Equal, SplitCount = 3 }, CancellationToken.None);

        Assert.Equal(3, checks.Count);
        Assert.Equal(100m, checks.Sum(c => c.Subtotal)); // parts sum exactly to subtotal
        // 100/3 = 33.33 each; remainder 0.01 → first check 33.34
        Assert.Equal(33.34m, checks[0].Subtotal);
        Assert.Equal(33.33m, checks[1].Subtotal);
        Assert.Equal(33.33m, checks[2].Subtotal);
    }

    // ----- Percentage --------------------------------------------------------------

    [Fact]
    public async Task Percentage_SplitsByShare()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 200m);

        var checks = await svc.CreateChecksAsync(orderId,
            new CreateChecksDto { Mode = SplitMode.Percentage, Percentages = new() { 60m, 40m } },
            CancellationToken.None);

        Assert.Equal(120m, checks[0].Subtotal);
        Assert.Equal(80m, checks[1].Subtotal);
        Assert.Equal(200m, checks.Sum(c => c.Subtotal));
    }

    [Fact]
    public async Task Percentage_NotSumming100_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 200m);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateChecksAsync(orderId,
                new CreateChecksDto { Mode = SplitMode.Percentage, Percentages = new() { 60m, 30m } },
                CancellationToken.None));
        Assert.Equal("ERR_PERCENTAGES_SUM", ex.ErrorCode);
    }

    // ----- Service charge opt-out --------------------------------------------------

    [Fact]
    public async Task ServiceCharge_OptOut_ZeroesItAndRecomputesTotal()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 100m);
        var checks = await svc.CreateChecksAsync(orderId, new CreateChecksDto { Mode = SplitMode.Single }, CancellationToken.None);

        var updated = await svc.SetServiceChargeAsync(checks[0].Id, optedOut: true, CancellationToken.None);

        Assert.Equal(0m, updated.ServiceChargeAmount);
        Assert.Equal(100m, updated.Total); // subtotal only (tax 0)
        Assert.True(updated.ServiceChargeOptedOut);
    }

    // ----- Pay / close -------------------------------------------------------------

    [Fact]
    public async Task Pay_LastCheck_ClosesOrder()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 100m);
        var checks = await svc.CreateChecksAsync(orderId,
            new CreateChecksDto { Mode = SplitMode.Equal, SplitCount = 2 }, CancellationToken.None);

        await svc.PayAsync(checks[0].Id, PaymentType.Cash, CancellationToken.None);
        var orderMid = await ctx.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.Open, orderMid!.Status); // still one open check

        await svc.PayAsync(checks[1].Id, PaymentType.Card, CancellationToken.None);
        var orderEnd = await ctx.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.Closed, orderEnd!.Status); // last check paid → order closed
        Assert.NotNull(orderEnd.ClosedAt);
    }

    [Fact]
    public async Task ReSplit_AfterPayment_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 100m);
        var checks = await svc.CreateChecksAsync(orderId,
            new CreateChecksDto { Mode = SplitMode.Equal, SplitCount = 2 }, CancellationToken.None);
        await svc.PayAsync(checks[0].Id, PaymentType.Cash, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateChecksAsync(orderId, new CreateChecksDto { Mode = SplitMode.Single }, CancellationToken.None));
        Assert.Equal("ERR_CHECK_ALREADY_PAID", ex.ErrorCode);
    }

    [Fact]
    public async Task ReSplit_WhenAllUnpaid_ReplacesChecks()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 100m);

        await svc.CreateChecksAsync(orderId, new CreateChecksDto { Mode = SplitMode.Equal, SplitCount = 4 }, CancellationToken.None);
        var redone = await svc.CreateChecksAsync(orderId, new CreateChecksDto { Mode = SplitMode.Single }, CancellationToken.None);

        Assert.Single(redone);
        Assert.Equal(1, await ctx.Checks.CountAsync()); // old 4 replaced
    }

    [Fact]
    public async Task Pay_AlreadyPaid_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var svc = Build(ctx);
        var (orderId, _) = await SeedOrderAsync(ctx, taxRate: 0m, 100m);
        var checks = await svc.CreateChecksAsync(orderId, new CreateChecksDto { Mode = SplitMode.Single }, CancellationToken.None);
        await svc.PayAsync(checks[0].Id, PaymentType.Cash, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.PayAsync(checks[0].Id, PaymentType.Cash, CancellationToken.None));
        Assert.Equal("ERR_CHECK_ALREADY_PAID", ex.ErrorCode);
    }
}
