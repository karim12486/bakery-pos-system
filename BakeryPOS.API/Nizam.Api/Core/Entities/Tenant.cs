namespace Nizam.Api.Core.Entities;

/// <summary>
/// A NIZAM customer — typically a single business (one bakery chain, one café group).
/// All other tenant-scoped entities reference this via <c>TenantId</c>.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    /// <summary>Display name shown to staff (e.g. "Karim's Bakery").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Slug used in URLs / receipts / brand display. URL-safe, lowercase.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>FK → <see cref="Core.Entities.Plan.Code"/>. Subscription plan code: <c>starter</c>, <c>growth</c>, <c>pro</c>.</summary>
    public string PlanCode { get; set; } = "starter";

    /// <summary>Billing cycle: <c>monthly</c> or <c>annual</c>. Drives invoice generation.</summary>
    public string BillingCycle { get; set; } = "monthly";

    /// <summary>UTC time when the trial expires; null after conversion to paid.</summary>
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>ISO-4217 currency code; defaults to EGP per Egyptian-first launch.</summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>IETF BCP 47 culture tag; defaults to Arabic (Egypt) per Egyptian-first launch.</summary>
    public string Locale { get; set; } = "ar-EG";

    /// <summary>Subscription status: <c>trialing</c>, <c>active</c>, <c>past_due</c>, <c>suspended</c>, <c>cancelled</c>.</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();

    /// <summary>Navigation to the current plan (loaded explicitly; not auto-included).</summary>
    public Plan? Plan { get; set; }
}
