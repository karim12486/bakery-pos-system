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

    /// <summary>Subscription plan code: <c>trial</c>, <c>starter</c>, <c>pro</c>, <c>enterprise</c>.</summary>
    public string Plan { get; set; } = "trial";

    /// <summary>ISO-4217 currency code; defaults to EGP per Egyptian-first launch.</summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>IETF BCP 47 culture tag; defaults to Arabic (Egypt) per Egyptian-first launch.</summary>
    public string Locale { get; set; } = "ar-EG";

    /// <summary>Soft-status: <c>active</c>, <c>suspended</c>, <c>trialing</c>, <c>cancelled</c>.</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
