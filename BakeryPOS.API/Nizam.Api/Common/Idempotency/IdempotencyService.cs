using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Common.Idempotency;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly AppDbContext _context;

    public IdempotencyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CachedResponse?> TryGetAsync(string endpoint, string key, CancellationToken ct = default)
    {
        var record = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Endpoint == endpoint && r.Key == key, ct);

        return record == null ? null : new CachedResponse(record.ResponseStatus, record.ResponseBody);
    }

    /// <summary>
    /// Stores the response under (TenantId, Endpoint, Key). If a concurrent request stored the
    /// SAME key first, the unique-index INSERT here throws <see cref="DbUpdateException"/>; we
    /// catch it and re-fetch the winning record, returning it to the caller. This is the
    /// "second writer loses, both readers get the same response" pattern (Stripe-style).
    /// </summary>
    /// <returns>
    /// The cached response that ultimately survives in the table. For the FIRST writer this is
    /// the response they just stored; for the loser of a race it's the response their peer stored.
    /// </returns>
    public async Task<CachedResponse> StoreAsync(string endpoint, string key, int statusCode, string body, CancellationToken ct = default)
    {
        var record = new IdempotencyRecord
        {
            Endpoint = endpoint,
            Key = key,
            ResponseStatus = statusCode,
            ResponseBody = body,
            CreatedAt = DateTime.UtcNow
            // TenantId auto-stamped by AppDbContext.SaveChanges
        };
        _context.IdempotencyRecords.Add(record);

        try
        {
            await _context.SaveChangesAsync(ct);
            return new CachedResponse(statusCode, body);
        }
        catch (DbUpdateException)
        {
            // Likely a unique-index violation from a concurrent peer that stored first.
            // Detach our duplicate to keep the change tracker clean, then re-fetch the winner.
            _context.Entry(record).State = EntityState.Detached;
            var winning = await TryGetAsync(endpoint, key, ct);
            if (winning != null) return winning;

            // Re-fetch came up empty — the DbUpdateException was something OTHER than a unique
            // conflict on this key. Rethrow so the caller sees the real failure.
            throw;
        }
    }
}
