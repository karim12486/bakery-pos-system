namespace Nizam.Api.Core.Entities;

/// <summary>
/// A per-tenant override of a plan feature — the sales escape hatch + add-on grant mechanism.
/// Layers on top of the tenant's plan: an <see cref="Enabled"/>=true row GRANTS a feature the
/// plan doesn't include (e.g. a Starter tenant who buys the Loyalty add-on); an
/// <see cref="Enabled"/>=false row REVOKES a feature the plan does include (rare; clawback /
/// dispute). Optionally time-limited via <see cref="ExpiresAt"/>.
///
/// <para>Resolved by <c>PlanService</c>: effective features = plan features ∪ enabled overrides
/// ∖ disabled overrides, considering only non-expired rows.</para>
/// </summary>
public class TenantFeatureOverride
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>The feature key this override affects (e.g. <c>loyalty</c>, <c>messaging_notifications</c>).</summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>True = grant the feature; false = revoke it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Optional expiry (UTC). Null = no expiry. Expired rows are ignored.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Audit context — why the override exists ("Loyalty add-on purchased", "Sales
    /// promised free modifiers", etc.).</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
