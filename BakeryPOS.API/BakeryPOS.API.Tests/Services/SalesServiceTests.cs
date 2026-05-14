using AutoMapper;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Validators;
using BakeryPOS.API.Mappers;
using BakeryPOS.API.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BakeryPOS.API.Tests.Services;

/// <summary>
/// Service-level unit tests using EF Core's in-memory provider.
/// Pattern for future per-service tests.
/// </summary>
public class SalesServiceTests
{
    private static IMapper BuildMapper()
    {
        // AutoMapper 15 resolves an ILoggerFactory from DI — add Logging before AddAutoMapper.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static IValidator<SaleForCreateDto> BuildValidator() => new SaleForCreateDtoValidator();

    private static async Task<(AppDbContext Ctx, User Cashier, Product P1)> SeedAsync()
    {
        var ctx = TestContextFactory.Create();

        var cashier = new User
        {
            Username = "cashier1",
            PasswordHash = "n/a",
            FullName = "Cashier One",
            IsActive = true,
            Permissions = UserPermissions.ProcessSales
        };
        ctx.Users.Add(cashier);

        var category = new Category { Name = "Cakes" };
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();

        var product = new Product
        {
            Name = "Chocolate Cupcake",
            Description = "",
            Price = 10m,
            CostPrice = 4m,
            StockQuantity = 5,
            CategoryId = category.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        // SalesService.CreateAsync now requires an open shift for the cashier (R3.2 wiring).
        ctx.Shifts.Add(new Shift
        {
            UserId = cashier.Id,
            BranchId = 1,
            OpeningFloat = 100m,
            OpenedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        return (ctx, cashier, product);
    }

    [Fact]
    public async Task CreateAsync_HappyPath_DecrementsStockAndReturnsSaleId()
    {
        var (ctx, cashier, product) = await SeedAsync();
        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());

        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 20m,
            SaleDetails = new List<SaleDetailForCreateDto>
            {
                new() { ProductId = product.Id, Quantity = 2 }
            }
        };

        var result = await svc.CreateAsync(dto, cashier.Username, CancellationToken.None);

        Assert.True(result.SaleId > 0);
        Assert.Equal(0m, result.Change);

        var refreshedProduct = await ctx.Products.FindAsync(product.Id);
        Assert.Equal(3, refreshedProduct!.StockQuantity);
        Assert.Single(await ctx.Sales.ToListAsync());
        Assert.Equal(1, await ctx.SaleDetails.CountAsync());     // one line item
        Assert.Equal(1, await ctx.StockMovements.CountAsync());  // one ledger entry per line
    }

    [Fact]
    public async Task CreateAsync_CashOverpayment_ReturnsChange()
    {
        var (ctx, cashier, product) = await SeedAsync();
        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());

        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 50m,                      // tendered far more than 20
            SaleDetails = new List<SaleDetailForCreateDto>
            {
                new() { ProductId = product.Id, Quantity = 2 } // total = 20
            }
        };

        var result = await svc.CreateAsync(dto, cashier.Username, CancellationToken.None);

        Assert.Equal(30m, result.Change);
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsDomainConflict()
    {
        var (ctx, cashier, product) = await SeedAsync(); // stock = 5
        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());

        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 100m,
            SaleDetails = new List<SaleDetailForCreateDto>
            {
                new() { ProductId = product.Id, Quantity = 10 } // > 5
            }
        };

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateAsync(dto, cashier.Username, CancellationToken.None));
        Assert.Equal("ERR_INSUFFICIENT_STOCK", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_PartialCashWithoutCustomer_ThrowsDomainException()
    {
        var (ctx, cashier, product) = await SeedAsync();
        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());

        // Cash payment less than total + no customer → service refuses to record the debt.
        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 5m,                       // less than total of 20
            SaleDetails = new List<SaleDetailForCreateDto>
            {
                new() { ProductId = product.Id, Quantity = 2 }
            }
        };

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateAsync(dto, cashier.Username, CancellationToken.None));
        Assert.Equal("ERR_CUSTOMER_REQUIRED_FOR_DEBT", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_UnknownProduct_ThrowsDomainException()
    {
        var (ctx, cashier, _) = await SeedAsync();
        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());

        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 100m,
            SaleDetails = new List<SaleDetailForCreateDto>
            {
                new() { ProductId = 9999, Quantity = 1 }
            }
        };

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateAsync(dto, cashier.Username, CancellationToken.None));
        Assert.Equal("ERR_PRODUCT_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_EmptySaleDetails_FailsValidation()
    {
        var (ctx, cashier, _) = await SeedAsync();
        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());

        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 0m,
            SaleDetails = new List<SaleDetailForCreateDto>()
        };

        // FluentValidation triggers; ProblemDetailsMiddleware turns this into a 422 at the
        // edge — but at the service level it's a plain ValidationException.
        await Assert.ThrowsAsync<ValidationException>(
            () => svc.CreateAsync(dto, cashier.Username, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithNoOpenShift_ThrowsDomainConflict()
    {
        // Seed everything except the open shift.
        var ctx = TestContextFactory.Create();
        var cashier = new User
        {
            Username = "no-shift-cashier",
            PasswordHash = "n/a",
            FullName = "No Shift",
            IsActive = true,
            Permissions = UserPermissions.ProcessSales
        };
        ctx.Users.Add(cashier);
        var category = new Category { Name = "Cakes" };
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();
        var product = new Product
        {
            Name = "Cupcake", Description = "", Price = 10m, CostPrice = 4m,
            StockQuantity = 5, CategoryId = category.Id, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();
        // (no Shift added)

        var svc = new SalesService(ctx, BuildMapper(), BuildValidator(), new NoOpAuditService());
        var dto = new SaleForCreateDto
        {
            PaymentMethod = PaymentType.Cash,
            AmountPaid = 100m,
            SaleDetails = new List<SaleDetailForCreateDto>
            {
                new() { ProductId = product.Id, Quantity = 1 }
            }
        };

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateAsync(dto, cashier.Username, CancellationToken.None));
        Assert.Equal("ERR_NO_OPEN_SHIFT", ex.ErrorCode);
    }
}
