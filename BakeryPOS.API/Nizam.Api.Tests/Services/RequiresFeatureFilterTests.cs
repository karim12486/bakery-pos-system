using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Attributes;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.Services.Plans;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RequiresFeatureFilter"/>. Verifies the filter passes for granted
/// features, throws the typed <see cref="PlanFeatureMissingException"/> for missing ones (so
/// ProblemDetailsMiddleware can emit a 403 with the right extensions), and works as expected
/// when no tenant is in scope.
/// </summary>
public class RequiresFeatureFilterTests
{
    private const int TenantId = 1;

    private static DbContextOptions<AppDbContext> OptionsFor(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<string> SeedPlanAsync(string planCode, params string[] features)
    {
        var dbName = $"req-feature-{Guid.NewGuid():N}";
        await using var ctx = new AppDbContext(OptionsFor(dbName), new AmbientTenant(null));
        var plan = new Plan
        {
            Code = planCode,
            Name = planCode,
            Description = "",
            MonthlyPriceEgp = 0,
            AnnualPriceEgp = 0,
            IsActive = true,
        };
        plan.Features = features.Select(f => new PlanFeature { PlanCode = planCode, FeatureKey = f, Plan = plan }).ToList();
        plan.Limits = new List<PlanLimit>();
        ctx.Plans.Add(plan);
        ctx.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Test",
            Slug = "test",
            PlanCode = planCode,
            BillingCycle = "monthly",
            Currency = "EGP",
            Locale = "ar-EG",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return dbName;
    }

    private static (RequiresFeatureFilter filter, AuthorizationFilterContext ctx) Build(
        string featureKey, string dbName, int? tenantId)
    {
        var ambient = new AmbientTenant(tenantId);
        var appDb = new AppDbContext(OptionsFor(dbName), ambient);
        var planService = new PlanService(
            appDb,
            ambient,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<PlanService>.Instance);

        var filter = new RequiresFeatureFilter(featureKey, planService);

        // Build a minimal AuthorizationFilterContext. We only need HttpContext.RequestAborted
        // to be readable; the rest is unused by the filter under test.
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var filterCtx = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        return (filter, filterCtx);
    }

    [Fact]
    public async Task GrantedFeature_DoesNotThrow_AllowsActionToProceed()
    {
        var db = await SeedPlanAsync("growth", "modifiers", "loyalty");
        var (filter, ctx) = Build("modifiers", db, TenantId);

        await filter.OnAuthorizationAsync(ctx);

        // No exception, Result not set => action proceeds.
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task MissingFeature_Throws_PlanFeatureMissingException()
    {
        var db = await SeedPlanAsync("starter"); // no features granted
        var (filter, ctx) = Build("modifiers", db, TenantId);

        var ex = await Assert.ThrowsAsync<PlanFeatureMissingException>(
            () => filter.OnAuthorizationAsync(ctx));

        Assert.Equal("modifiers", ex.FeatureKey);
        Assert.Equal("starter", ex.CurrentPlan);
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("ERR_PLAN_FEATURE_MISSING", ex.ErrorCode);

        // Extensions used by the frontend to render upgrade CTAs.
        Assert.NotNull(ex.Extensions);
        Assert.Equal("modifiers", ex.Extensions!["requiredFeature"]);
        Assert.Equal("starter", ex.Extensions["currentPlan"]);
    }

    [Fact]
    public async Task NoTenantInScope_Throws_WithNonePlaceholder()
    {
        // Anonymous request hits an action with [RequiresFeature(...)] (probably because
        // [Authorize] was forgotten). HasFeatureAsync returns false → we throw, with
        // a "(none)" placeholder for currentPlan so the error is still informative.
        var db = await SeedPlanAsync("growth", "modifiers");
        var (filter, ctx) = Build("modifiers", db, tenantId: null);

        var ex = await Assert.ThrowsAsync<PlanFeatureMissingException>(
            () => filter.OnAuthorizationAsync(ctx));

        Assert.Equal("modifiers", ex.FeatureKey);
        Assert.Equal("(none)", ex.CurrentPlan);
    }

    [Fact]
    public async Task MultipleAttributesOnOneAction_AllMustPass()
    {
        // [RequiresFeature("modifiers")] + [RequiresFeature("loyalty")] means BOTH required.
        // Simulate by running two filters; first failure throws.
        var db = await SeedPlanAsync("growth", "modifiers"); // loyalty NOT granted

        var (filter1, ctx1) = Build("modifiers", db, TenantId);
        await filter1.OnAuthorizationAsync(ctx1); // passes

        var (filter2, ctx2) = Build("loyalty", db, TenantId);
        await Assert.ThrowsAsync<PlanFeatureMissingException>(
            () => filter2.OnAuthorizationAsync(ctx2)); // fails — AND semantics
    }

    [Fact]
    public void Constructor_RejectsBlankFeatureKey()
    {
        Assert.Throws<ArgumentException>(() => new RequiresFeatureAttribute(""));
        Assert.Throws<ArgumentException>(() => new RequiresFeatureAttribute("   "));
        Assert.Throws<ArgumentException>(() => new RequiresFeatureAttribute(null!));
    }

    [Fact]
    public void Attribute_StoresFeatureKey_ForCodeReviewVisibility()
    {
        var attr = new RequiresFeatureAttribute("modifiers");
        Assert.Equal("modifiers", attr.FeatureKey);
    }
}
