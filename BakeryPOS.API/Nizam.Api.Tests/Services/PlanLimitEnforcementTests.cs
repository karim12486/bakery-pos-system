using AutoMapper;
using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Mappers;
using Nizam.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Service-level tests for plan-limit enforcement on the create paths
/// (AdminService.CreateUserAsync — max_users; ProductService.CreateAsync — max_products).
/// Uses <see cref="StubPlanService"/> to control limits without standing up real plan data.
/// </summary>
public class PlanLimitEnforcementTests
{
    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperProfiles).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static AdminService BuildAdminService(AppDbContext ctx, StubPlanService plans)
        => new AdminService(
            ctx,
            new PasswordService(),
            BuildMapper(),
            new NoOpAuditService(),
            plans);

    private static ProductService BuildProductService(AppDbContext ctx, StubPlanService plans)
        => new ProductService(ctx, BuildMapper(), plans);

    // ----- AdminService — max_users -----------------------------------------------------

    [Fact]
    public async Task CreateUserAsync_UnderLimit_Succeeds()
    {
        using var ctx = TestContextFactory.Create();
        var plans = new StubPlanService { Limits = { ["max_users"] = 3 } };
        var svc = BuildAdminService(ctx, plans);

        await svc.CreateUserAsync(NewUserDto("alice"), CancellationToken.None);

        Assert.Equal(1, ctx.Users.Count(u => u.IsActive));
    }

    [Fact]
    public async Task CreateUserAsync_AtLimit_ThrowsPlanLimit()
    {
        using var ctx = TestContextFactory.Create();
        SeedActiveUsers(ctx, count: 3);

        var plans = new StubPlanService { PlanCode = "starter", Limits = { ["max_users"] = 3 } };
        var svc = BuildAdminService(ctx, plans);

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => svc.CreateUserAsync(NewUserDto("alice"), CancellationToken.None));

        Assert.Equal("max_users", ex.LimitKey);
        Assert.Equal(3, ex.CurrentCount);
        Assert.Equal(3, ex.MaxAllowed);
        Assert.Equal("starter", ex.CurrentPlan);
        Assert.Equal(402, ex.StatusCode);

        // Side effect: no new user was added.
        Assert.Equal(3, ctx.Users.Count(u => u.IsActive));
    }

    [Fact]
    public async Task CreateUserAsync_DeactivatedUsers_DoNotCountTowardLimit()
    {
        // Industry "seats" model: deactivating a user frees their slot.
        using var ctx = TestContextFactory.Create();
        SeedActiveUsers(ctx, count: 2);
        SeedDeactivatedUsers(ctx, count: 5); // shouldn't matter

        var plans = new StubPlanService { Limits = { ["max_users"] = 3 } };
        var svc = BuildAdminService(ctx, plans);

        // 2 active + room for 1 more = should succeed
        await svc.CreateUserAsync(NewUserDto("newbie"), CancellationToken.None);
        Assert.Equal(3, ctx.Users.Count(u => u.IsActive));
    }

    [Fact]
    public async Task CreateUserAsync_UnlimitedPlan_AlwaysSucceeds()
    {
        using var ctx = TestContextFactory.Create();
        SeedActiveUsers(ctx, count: 50);

        var plans = new StubPlanService { Limits = { ["max_users"] = -1 } }; // -1 = unlimited
        var svc = BuildAdminService(ctx, plans);

        await svc.CreateUserAsync(NewUserDto("user-51"), CancellationToken.None);
        Assert.Equal(51, ctx.Users.Count(u => u.IsActive));
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_StillThrowsDuplicateError_NotLimitError()
    {
        // Duplicate-username check should run first; we shouldn't even hit the limit check.
        using var ctx = TestContextFactory.Create();
        SeedActiveUsers(ctx, count: 1, usernames: new[] { "alice" });

        var plans = new StubPlanService { Limits = { ["max_users"] = 1 } }; // would-fail if reached
        var svc = BuildAdminService(ctx, plans);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.CreateUserAsync(NewUserDto("alice"), CancellationToken.None));

        Assert.Equal("ERR_USERNAME_TAKEN", ex.ErrorCode);
    }

    // ----- ProductService — max_products ------------------------------------------------

    [Fact]
    public async Task CreateProductAsync_UnderLimit_Succeeds()
    {
        using var ctx = TestContextFactory.Create();
        SeedCategory(ctx, id: 1);

        var plans = new StubPlanService { Limits = { ["max_products"] = 50 } };
        var svc = BuildProductService(ctx, plans);

        await svc.CreateAsync(NewProductDto("Latte", categoryId: 1), CancellationToken.None);
        Assert.Equal(1, ctx.Products.Count(p => p.IsActive));
    }

    [Fact]
    public async Task CreateProductAsync_AtLimit_ThrowsPlanLimit()
    {
        using var ctx = TestContextFactory.Create();
        SeedCategory(ctx, id: 1);
        SeedActiveProducts(ctx, count: 50, categoryId: 1);

        var plans = new StubPlanService { PlanCode = "starter", Limits = { ["max_products"] = 50 } };
        var svc = BuildProductService(ctx, plans);

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => svc.CreateAsync(NewProductDto("Latte", categoryId: 1), CancellationToken.None));

        Assert.Equal("max_products", ex.LimitKey);
        Assert.Equal(50, ex.CurrentCount);
        Assert.Equal(50, ex.MaxAllowed);
        Assert.Equal(50, ctx.Products.Count(p => p.IsActive));
    }

    [Fact]
    public async Task CreateProductAsync_SoftDeletedProducts_DoNotCountTowardLimit()
    {
        // Soft-deleted (IsActive=false) products free up catalog slots.
        using var ctx = TestContextFactory.Create();
        SeedCategory(ctx, id: 1);
        SeedActiveProducts(ctx, count: 49, categoryId: 1);
        SeedDeletedProducts(ctx, count: 100, categoryId: 1); // shouldn't count

        var plans = new StubPlanService { Limits = { ["max_products"] = 50 } };
        var svc = BuildProductService(ctx, plans);

        // 49 active + 1 = exactly 50, still under limit (we check BEFORE adding, currentCount=49)
        await svc.CreateAsync(NewProductDto("Latte", categoryId: 1), CancellationToken.None);
        Assert.Equal(50, ctx.Products.Count(p => p.IsActive));
    }

    [Fact]
    public async Task CreateProductAsync_UnlimitedPlan_AlwaysSucceeds()
    {
        using var ctx = TestContextFactory.Create();
        SeedCategory(ctx, id: 1);
        SeedActiveProducts(ctx, count: 1000, categoryId: 1);

        var plans = new StubPlanService { Limits = { ["max_products"] = -1 } };
        var svc = BuildProductService(ctx, plans);

        await svc.CreateAsync(NewProductDto("1001st", categoryId: 1), CancellationToken.None);
        Assert.Equal(1001, ctx.Products.Count(p => p.IsActive));
    }

    [Fact]
    public async Task CreateProductAsync_CategoryNotFound_ThrowsBefore_LimitCheck()
    {
        // Validation order: missing category should fail first, even if limit would also fail.
        // This isn't just nice-to-have — it gives the user the most informative error.
        using var ctx = TestContextFactory.Create();
        var plans = new StubPlanService { Limits = { ["max_products"] = 0 } }; // would block anyway
        var svc = BuildProductService(ctx, plans);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateAsync(NewProductDto("Latte", categoryId: 999), CancellationToken.None));

        Assert.Equal("ERR_CATEGORY_NOT_FOUND", ex.ErrorCode);
    }

    // ----- Helpers ----------------------------------------------------------------------

    private static UserForCreationDto NewUserDto(string username) => new()
    {
        Username = username,
        Password = "Passw0rd!",
        FullName = "Test User",
        Permissions = UserPermissions.ProcessSales,
    };

    private static ProductForCreateDto NewProductDto(string name, int categoryId) => new()
    {
        Name = name,
        Price = 25m,
        CostPrice = 10m,
        StockQuantity = 5,
        CategoryId = categoryId,
        IsSpecial = false,
    };

    private static void SeedCategory(AppDbContext ctx, int id)
    {
        ctx.Categories.Add(new Category { Id = id, Name = "Test Cat" });
        ctx.SaveChanges();
    }

    private static void SeedActiveUsers(AppDbContext ctx, int count, string[]? usernames = null)
    {
        for (var i = 0; i < count; i++)
        {
            ctx.Users.Add(new User
            {
                Username = usernames?[i] ?? $"u{i}",
                PasswordHash = "n/a",
                FullName = $"User {i}",
                IsActive = true,
                Permissions = UserPermissions.ProcessSales,
            });
        }
        ctx.SaveChanges();
    }

    private static void SeedDeactivatedUsers(AppDbContext ctx, int count)
    {
        for (var i = 0; i < count; i++)
        {
            ctx.Users.Add(new User
            {
                Username = $"deact{i}",
                PasswordHash = "n/a",
                FullName = $"Deactivated {i}",
                IsActive = false,
                Permissions = UserPermissions.ProcessSales,
            });
        }
        ctx.SaveChanges();
    }

    private static void SeedActiveProducts(AppDbContext ctx, int count, int categoryId)
    {
        for (var i = 0; i < count; i++)
        {
            ctx.Products.Add(new Product
            {
                Name = $"p{i}",
                Description = "",
                Price = 1m,
                CostPrice = 1m,
                StockQuantity = 1,
                CategoryId = categoryId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }
        ctx.SaveChanges();
    }

    private static void SeedDeletedProducts(AppDbContext ctx, int count, int categoryId)
    {
        for (var i = 0; i < count; i++)
        {
            ctx.Products.Add(new Product
            {
                Name = $"del{i}",
                Description = "",
                Price = 1m,
                CostPrice = 1m,
                StockQuantity = 0,
                CategoryId = categoryId,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
            });
        }
        ctx.SaveChanges();
    }
}
