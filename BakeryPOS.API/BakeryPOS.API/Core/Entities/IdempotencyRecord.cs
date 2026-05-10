namespace BakeryPOS.API.Core.Entities;

/// <summary>
/// Stores the result of a previously-completed write operation keyed by a client-supplied
/// idempotency key. Allows safe retry of POST endpoints (e.g. cashier double-tap, offline-POS
/// sync, network blip) — the second call returns the original response without re-executing.
/// </summary>
public class IdempotencyRecord
{
    public int Id { get; set; }

    /// <summary>Client-generated key (typically a UUID). Unique per tenant once tenancy lands.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Endpoint identifier, e.g. <c>POST /api/sales</c>. Lets the same key be reused across endpoints.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>HTTP status of the original response.</summary>
    public int ResponseStatus { get; set; }

    /// <summary>JSON-serialised body of the original response.</summary>
    public string ResponseBody { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
