namespace Nizam.Api.Common.Errors;

/// <summary>
/// 403 Forbidden — current plan doesn't grant the requested feature. Includes the requested
/// <c>featureKey</c> and the tenant's <c>currentPlan</c> so the frontend can render a
/// "Upgrade to Growth/Pro" CTA with the right context.
/// </summary>
public sealed class PlanFeatureMissingException : DomainException
{
    public string FeatureKey { get; }
    public string CurrentPlan { get; }

    public PlanFeatureMissingException(string featureKey, string currentPlan)
        : base(
            errorCode: "ERR_PLAN_FEATURE_MISSING",
            message: $"Your plan ({currentPlan}) does not include the '{featureKey}' feature.",
            statusCode: StatusCodes.Status403Forbidden,
            extensions: new Dictionary<string, object?>
            {
                ["requiredFeature"] = featureKey,
                ["currentPlan"] = currentPlan,
            })
    {
        FeatureKey = featureKey;
        CurrentPlan = currentPlan;
    }
}

/// <summary>
/// 402 Payment Required — operation would push the tenant past their plan limit. Includes
/// the <c>limitKey</c>, <c>currentCount</c> and <c>maxAllowed</c> so the UI can show a
/// useful error ("You've reached 3 of 3 branches — upgrade to Pro to add more").
/// </summary>
public sealed class PlanLimitExceededException : DomainException
{
    public string LimitKey { get; }
    public int CurrentCount { get; }
    public int MaxAllowed { get; }
    public string CurrentPlan { get; }

    public PlanLimitExceededException(string limitKey, int currentCount, int maxAllowed, string currentPlan)
        : base(
            errorCode: "ERR_PLAN_LIMIT_EXCEEDED",
            message: $"Plan limit reached for '{limitKey}': {currentCount}/{maxAllowed} on plan '{currentPlan}'.",
            statusCode: StatusCodes.Status402PaymentRequired,
            extensions: new Dictionary<string, object?>
            {
                ["limitKey"] = limitKey,
                ["currentCount"] = currentCount,
                ["maxAllowed"] = maxAllowed,
                ["currentPlan"] = currentPlan,
            })
    {
        LimitKey = limitKey;
        CurrentCount = currentCount;
        MaxAllowed = maxAllowed;
        CurrentPlan = currentPlan;
    }
}
