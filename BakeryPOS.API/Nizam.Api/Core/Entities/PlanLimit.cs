namespace Nizam.Api.Core.Entities;

/// <summary>
/// A numeric limit imposed by a plan. <see cref="Value"/> = <c>-1</c> means "unlimited" by convention.
///
/// Known limit keys:
/// <list type="bullet">
///   <item><c>max_branches</c> — Starter 1, Growth 3, Pro 10 (extra via add-on)</item>
///   <item><c>max_users</c> — Starter 3, Growth 15, Pro -1 (unlimited)</item>
///   <item><c>max_products</c> — Starter 50, Growth 200, Pro -1</item>
///   <item><c>audit_log_retention_days</c> — Starter 7, Growth 90, Pro 365</item>
///   <item><c>monthly_ai_tokens</c> — only meaningful when <c>ai_insights</c> feature is granted</item>
/// </list>
/// </summary>
public class PlanLimit
{
    public int Id { get; set; }

    /// <summary>FK → <see cref="Plan.Code"/>.</summary>
    public string PlanCode { get; set; } = string.Empty;

    /// <summary>Stable string key checked in code via <c>GetLimit(key)</c>.</summary>
    public string LimitKey { get; set; } = string.Empty;

    /// <summary>Integer limit. Use <c>-1</c> for unlimited.</summary>
    public int Value { get; set; }

    public Plan? Plan { get; set; }
}
