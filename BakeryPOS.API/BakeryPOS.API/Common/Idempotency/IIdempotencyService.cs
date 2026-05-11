namespace BakeryPOS.API.Common.Idempotency;

/// <summary>
/// Hooks for safe retry of write endpoints. Pattern:
/// <code>
/// var cached = await idem.TryGetAsync(endpoint, key, ct);
/// if (cached != null) return cached;          // replay original response
/// var result = await DoTheWork(...);
/// var stored = await idem.StoreAsync(endpoint, key, status, result, ct);
/// return stored;  // returns OUR response on first-writer-win, or the winner's response on race
/// </code>
/// Today the key is provided via the <c>Idempotency-Key</c> request header. Under multi-tenancy
/// the unique index is <c>(TenantId, Endpoint, Key)</c> — different tenants can use overlapping keys.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>Returns the cached response body if a record exists for (endpoint, key); null otherwise.</summary>
    Task<CachedResponse?> TryGetAsync(string endpoint, string key, CancellationToken ct = default);

    /// <summary>
    /// Stores the response under (TenantId, Endpoint, Key). On a concurrent-write race (both
    /// callers passed TryGet before either stored), the loser doesn't throw — instead it
    /// transparently fetches the winning record and returns it. Caller sees a consistent
    /// "first writer wins" contract.
    /// </summary>
    Task<CachedResponse> StoreAsync(string endpoint, string key, int statusCode, string body, CancellationToken ct = default);
}

public sealed record CachedResponse(int StatusCode, string Body);
