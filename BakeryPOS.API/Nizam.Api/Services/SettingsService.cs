using System.Globalization;
using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ISettingsService
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct);
    Task<string?> GetAsync(string key, CancellationToken ct);
    Task SetAsync(string key, string value, CancellationToken ct);
    Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct);
    Task<TenantSettingsDto> GetWellKnownAsync(CancellationToken ct);
}

/// <summary>
/// Well-known setting keys. Adding a new key is a one-line addition here + an accessor
/// helper. No migration required (Settings table holds arbitrary key/value pairs).
/// </summary>
public static class SettingKeys
{
    public const string BusinessName = "business.name";
    public const string TaxRate = "tax.rate";                 // 0..1 decimal, e.g. 0.14 for 14% VAT
    public const string CurrencyCode = "currency.code";        // ISO-4217, e.g. "EGP"
    public const string ReceiptHeader = "receipt.header";
    public const string ReceiptFooter = "receipt.footer";
    public const string DefaultLocale = "locale.default";      // BCP-47, e.g. "ar-EG"
    public const string BrandLogoUrl = "brand.logoUrl";
}

public sealed class SettingsService : ISettingsService
{
    private readonly AppDbContext _context;

    public SettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct)
    {
        var rows = await _context.Settings.ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Value);
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        var row = await _context.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return row?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var row = await _context.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row != null)
        {
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
                // TenantId auto-stamped
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var keys = values.Keys.ToList();
        var existing = await _context.Settings.Where(s => keys.Contains(s.Key)).ToListAsync(ct);
        var existingByKey = existing.ToDictionary(s => s.Key);
        var now = DateTime.UtcNow;

        foreach (var (key, value) in values)
        {
            if (existingByKey.TryGetValue(key, out var row))
            {
                row.Value = value;
                row.UpdatedAt = now;
            }
            else
            {
                _context.Settings.Add(new Setting { Key = key, Value = value, UpdatedAt = now });
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<TenantSettingsDto> GetWellKnownAsync(CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return new TenantSettingsDto
        {
            BusinessName = all.GetValueOrDefault(SettingKeys.BusinessName),
            TaxRate = TryDecimal(all.GetValueOrDefault(SettingKeys.TaxRate)),
            CurrencyCode = all.GetValueOrDefault(SettingKeys.CurrencyCode),
            ReceiptHeader = all.GetValueOrDefault(SettingKeys.ReceiptHeader),
            ReceiptFooter = all.GetValueOrDefault(SettingKeys.ReceiptFooter),
            DefaultLocale = all.GetValueOrDefault(SettingKeys.DefaultLocale),
            BrandLogoUrl = all.GetValueOrDefault(SettingKeys.BrandLogoUrl)
        };
    }

    private static decimal? TryDecimal(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
}
