using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.Services.Plans;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services.Jobs;

/// <summary>
/// Daily trial housekeeping. A tenant is "on trial" while <see cref="Tenant.TrialEndsAt"/> is
/// non-null; when that moment passes and they haven't converted, downgrade them to Starter —
/// data stays, they just lose the paid features (the Egyptian-market-friendly soft landing, vs
/// locking them out). Conversion (a real plan change via super-admin / billing) clears
/// <c>TrialEndsAt</c>, so paying customers are never touched here.
/// </summary>
public sealed class TrialDowngradeJob
{
    public const string RecurringJobId = "trial-downgrade-daily";
    public const string Cron = "15 0 * * *"; // 00:15 UTC daily

    /// <summary>The plan expired trials fall back to.</summary>
    public const string FallbackPlan = "starter";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrialDowngradeJob> _logger;

    public TrialDowngradeJob(IServiceScopeFactory scopeFactory, ILogger<TrialDowngradeJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 300, 1800 })]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = scope.ServiceProvider.GetRequiredService<IPlanService>();

        var now = DateTime.UtcNow;
        var expired = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.TrialEndsAt != null && t.TrialEndsAt < now && t.PlanCode != FallbackPlan)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var tenant in expired)
        {
            tenant.PlanCode = FallbackPlan;
            tenant.TrialEndsAt = null; // processed; don't revisit
        }
        await db.SaveChangesAsync(ct);

        // Bust each tenant's cached entitlement snapshot so the downgrade takes effect at once.
        foreach (var tenant in expired) plans.InvalidateCache(tenant.Id);

        _logger.LogInformation("Downgraded {Count} expired-trial tenant(s) to {Plan}",
            expired.Count, FallbackPlan);
    }
}
