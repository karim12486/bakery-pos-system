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
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PublicOrderService"/> — a seated guest submits items from the QR
/// menu; they land Pending on the table's open order and flag it customer-submitted.
/// </summary>
public class PublicOrderServiceTests
{
    private const int TenantId = TestContextFactory.DefaultTenantId; // 1
    private const string Slug = "acme";
    private const string Server = "server1";

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    /// <summary>Builds the public order service over a context whose ICurrentTenant starts
    /// unset (anonymous) — the service sets the override itself.</summary>
    private static PublicOrderService Build(AppDbContext ctx, CurrentTenant tenant)
    {
        var kds = new KdsService(ctx, tenant, new FakeKdsHubContext(), new NoOpAuditService());
        var dineIn = new DineInService(ctx, BuildMapper(), new ModifierApplicationService(ctx), kds, new OrderStateMachine());
        return new PublicOrderService(ctx, tenant, dineIn);
    }

    /// <summary>Seeds tenant (qr_table_menu granted) + branch + table + product. Returns
    /// (tableId, productId). The order's session is created via dine-in seating.</summary>
    private static async Task<(int TableId, int ProductId)> SeedAsync(AppDbContext ctx, bool grantFeature = true)
    {
        ctx.Plans.Add(new Plan { Code = "pro", Name = "Pro", MonthlyPriceEgp = 2999, AnnualPriceEgp = 29990 });
        if (grantFeature)
            ctx.PlanFeatures.Add(new PlanFeature { PlanCode = "pro", FeatureKey = "qr_table_menu" });
        ctx.Tenants.Add(new Tenant
        {
            Id = TenantId, Name = "Acme", Slug = Slug, PlanCode = "pro",
            BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active", CreatedAt = DateTime.UtcNow,
        });
        ctx.Users.Add(new User
        {
            Username = Server, PasswordHash = "n/a", FullName = "Server", IsActive = true,
            Permissions = UserPermissions.ProcessSales,
        });
        var branch = new Branch { Name = "Main", Timezone = "Africa/Cairo", IsActive = true };
        ctx.Branches.Add(branch);
        var cat = new Category { Name = "Food" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();
        var area = new Area { BranchId = branch.Id, Name = "Indoor", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ctx.Areas.Add(area);
        var product = new Product
        {
            Name = "Burger", Description = "", Price = 60m, CostPrice = 20m, StockQuantity = 100,
            CategoryId = cat.Id, IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();
        var table = new Table
        {
            BranchId = branch.Id, AreaId = area.Id, Name = "T-1", Capacity = 4,
            Status = TableStatus.Free, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        ctx.Tables.Add(table);
        await ctx.SaveChangesAsync();
        return (table.Id, product.Id);
    }

    private static PublicOrderRequest Request(int tableId, int productId, int qty = 1) => new()
    {
        TableId = tableId,
        Order = new AddOrderItemsDto { Items = new() { new() { ProductId = productId, Quantity = qty } } },
    };

    [Fact]
    public async Task Submit_WithOpenSession_AppendsPending_FlagsCustomerSubmitted()
    {
        using var ctx = TestContextFactory.Create();
        var (tableId, productId) = await SeedAsync(ctx);
        var tenant = new CurrentTenant(new TestHttpContextAccessor());

        // A server seats the table first (establishes the open session + order).
        var seatKds = new KdsService(ctx, new AmbientTenant(TenantId), new FakeKdsHubContext(), new NoOpAuditService());
        var seatDineIn = new DineInService(ctx, BuildMapper(), new ModifierApplicationService(ctx), seatKds, new OrderStateMachine());
        await seatDineIn.SeatAsync(new SeatGuestsDto { TableId = tableId, GuestCount = 2 }, Server, CancellationToken.None);

        var svc = Build(ctx, tenant);
        var result = await svc.SubmitAsync(Slug, Request(tableId, productId, qty: 2), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Pending", result.Items[0].Status);
        Assert.Equal(120m, result.Subtotal); // 2 × 60

        var order = await ctx.Orders.FindAsync(result.OrderId);
        Assert.True(order!.IsCustomerSubmitted);
    }

    [Fact]
    public async Task Submit_NoOpenSession_Throws()
    {
        using var ctx = TestContextFactory.Create();
        var (tableId, productId) = await SeedAsync(ctx);
        var svc = Build(ctx, new CurrentTenant(new TestHttpContextAccessor()));

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.SubmitAsync(Slug, Request(tableId, productId), CancellationToken.None));
        Assert.Equal("ERR_NO_OPEN_SESSION", ex.ErrorCode);
    }

    [Fact]
    public async Task Submit_UnknownSlug_Throws404()
    {
        using var ctx = TestContextFactory.Create();
        var (tableId, productId) = await SeedAsync(ctx);
        var svc = Build(ctx, new CurrentTenant(new TestHttpContextAccessor()));

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.SubmitAsync("nope", Request(tableId, productId), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_FeatureNotGranted_Throws404()
    {
        using var ctx = TestContextFactory.Create();
        var (tableId, productId) = await SeedAsync(ctx, grantFeature: false);
        var svc = Build(ctx, new CurrentTenant(new TestHttpContextAccessor()));

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => svc.SubmitAsync(Slug, Request(tableId, productId), CancellationToken.None));
    }

    /// <summary>Minimal IHttpContextAccessor with no HttpContext — CurrentTenant falls back to
    /// its override (which the service sets).</summary>
    private sealed class TestHttpContextAccessor : Microsoft.AspNetCore.Http.IHttpContextAccessor
    {
        public Microsoft.AspNetCore.Http.HttpContext? HttpContext { get; set; }
    }
}
