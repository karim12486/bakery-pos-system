using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Services.Plans;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Minimal <see cref="IPlanService"/> stub for service unit tests that want to control
/// plan behaviour explicitly (without standing up a full Plan + Tenant in EF). Limits
/// default to "unlimited"; set entries on <see cref="Limits"/> to enforce a specific cap.
/// </summary>
internal sealed class StubPlanService : IPlanService
{
    /// <summary>Limits by key. Missing keys = unlimited (matches PlanService convention).</summary>
    public Dictionary<string, int> Limits { get; } = new(StringComparer.Ordinal);

    /// <summary>Features the stub grants. Empty by default.</summary>
    public HashSet<string> Features { get; } = new(StringComparer.Ordinal);

    /// <summary>The plan code returned in thrown exceptions.</summary>
    public string PlanCode { get; set; } = "test";

    public Task<Plan?> GetCurrentPlanAsync(CancellationToken ct = default)
        => Task.FromResult<Plan?>(new Plan { Code = PlanCode, Name = PlanCode });

    public Task<bool> HasFeatureAsync(string featureKey, CancellationToken ct = default)
        => Task.FromResult(Features.Contains(featureKey));

    public Task<int> GetLimitAsync(string limitKey, CancellationToken ct = default)
        => Task.FromResult(Limits.TryGetValue(limitKey, out var v) ? v : -1);

    public Task EnsureWithinLimitAsync(string limitKey, int currentCount, CancellationToken ct = default)
    {
        if (!Limits.TryGetValue(limitKey, out var max)) return Task.CompletedTask; // unlimited
        if (max == -1) return Task.CompletedTask;
        if (currentCount < max) return Task.CompletedTask;
        throw new PlanLimitExceededException(limitKey, currentCount, max, PlanCode);
    }

    public Task<IReadOnlyList<string>> GetGrantedFeaturesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Features.ToList());

    public void InvalidateCache(int tenantId) { /* no-op */ }
}
