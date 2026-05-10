namespace BakeryPOS.API.Common.Idempotency;

/// <summary>
/// Hooks for safe retry of write endpoints. Pattern:
/// <code>
/// var cached = await idem.TryGetAsync(endpoint, key, ct);
/// if (cached != null) return cached;          // replay original response
/// var result = await DoTheWork(...);
/// await idem.StoreAsync(endpoint, key, status, result, ct);
/// return result;
/// </code>
/// Today the key is provided via the <c>Idempotency-Key</c> request header. When tenancy
/// lands the unique index becomes <c>(TenantId, Endpoint, Key)</c>.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>Returns the cached response body if a record exists for (endpoint, key); null otherwise.</summary>
    Task<CachedResponse?> TryGetAsync(string endpoint, string key, CancellationToken ct = default);

    /// <summary>Stores the response. Throws on duplicate (endpoint, key) — caller should TryGet first.</summary>
    Task StoreAsync(string endpoint, string key, int statusCode, string body, CancellationToken ct = default);
}

public sealed record CachedResponse(int StatusCode, string Body);
