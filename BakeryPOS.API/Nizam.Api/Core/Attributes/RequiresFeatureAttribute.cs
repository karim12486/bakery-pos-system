using Nizam.Api.Common.Errors;
using Nizam.Api.Services.Plans;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Nizam.Api.Core.Attributes;

/// <summary>
/// <c>[RequiresFeature("modifiers")]</c> on a controller or action — gates the endpoint
/// behind a plan feature flag.
///
/// <para>Resolves <see cref="IPlanService"/> from DI, calls <c>HasFeatureAsync(featureKey)</c>,
/// and short-circuits with a <see cref="PlanFeatureMissingException"/> (403 ProblemDetails
/// with <c>requiredFeature</c> + <c>currentPlan</c> extensions) if the current tenant's
/// plan doesn't grant it.</para>
///
/// <para><b>Stackable, AND semantics</b>: multiple <c>[RequiresFeature(...)]</c> attributes
/// on the same target all run; the first one that fails throws. No way to express OR
/// (combine in a single check at the service layer if you need it).</para>
///
/// <para>Runs as an <see cref="IAsyncAuthorizationFilter"/> — earliest in the filter
/// pipeline, before model binding. Cheap feature checks shouldn't pay for request body
/// deserialization if they'll fail anyway.</para>
///
/// <para><b>Auth assumption</b>: pair with <c>[Authorize]</c>. Without auth, no tenant
/// is in scope, so <c>HasFeatureAsync</c> returns false and the filter rejects — but
/// you'll get a confusing "feature missing" 403 instead of an "auth required" 401.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresFeatureAttribute : TypeFilterAttribute
{
    /// <summary>The feature key (e.g. <c>modifiers</c>, <c>loyalty</c>, <c>tables</c>).</summary>
    public string FeatureKey { get; }

    public RequiresFeatureAttribute(string featureKey) : base(typeof(RequiresFeatureFilter))
    {
        if (string.IsNullOrWhiteSpace(featureKey))
            throw new ArgumentException("Feature key is required.", nameof(featureKey));
        FeatureKey = featureKey;
        Arguments = new object[] { featureKey };
    }
}

/// <summary>The actual filter instantiated by DI. Constructed per-request with the feature
/// key baked in via <see cref="RequiresFeatureAttribute.Arguments"/>.</summary>
public sealed class RequiresFeatureFilter : IAsyncAuthorizationFilter
{
    private readonly string _featureKey;
    private readonly IPlanService _plans;

    public RequiresFeatureFilter(string featureKey, IPlanService plans)
    {
        _featureKey = featureKey;
        _plans = plans;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (await _plans.HasFeatureAsync(_featureKey, context.HttpContext.RequestAborted))
        {
            return; // green light — feature granted, action proceeds
        }

        // Build a typed exception so the ProblemDetailsMiddleware emits the right JSON
        // (requiredFeature + currentPlan extensions). Throwing here propagates up to the
        // middleware that wraps the entire pipeline.
        var plan = await _plans.GetCurrentPlanAsync(context.HttpContext.RequestAborted);
        throw new PlanFeatureMissingException(_featureKey, plan?.Code ?? "(none)");
    }
}
