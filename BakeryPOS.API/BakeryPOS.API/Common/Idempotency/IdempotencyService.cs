using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Common.Idempotency;

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

    public async Task StoreAsync(string endpoint, string key, int statusCode, string body, CancellationToken ct = default)
    {
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Endpoint = endpoint,
            Key = key,
            ResponseStatus = statusCode,
            ResponseBody = body,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);
    }
}
