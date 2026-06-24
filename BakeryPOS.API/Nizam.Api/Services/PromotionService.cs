using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IPromotionService
{
    Task<IReadOnlyList<PromotionDto>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<PromotionDto?> GetAsync(int id, CancellationToken ct);
    Task<PromotionDto> CreateAsync(PromotionForCreateDto dto, CancellationToken ct);
    Task<PromotionDto> UpdateAsync(int id, PromotionForUpdateDto dto, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);

    /// <summary>Evaluates a promo (by code, or the best auto-apply promo when code is null)
    /// against an order subtotal. Returns null if nothing applies. Throws
    /// <see cref="DomainException"/> when a SPECIFIC code is given but invalid (expired, below
    /// minimum, exhausted) so the cashier sees why.</summary>
    Task<PromotionApplyResultDto?> EvaluateAsync(string? code, decimal subtotal, CancellationToken ct);

    /// <summary>Increments a promo's redemption counter (called when a sale using it commits).</summary>
    Task RecordRedemptionAsync(int promotionId, CancellationToken ct);
}

public sealed class PromotionService : IPromotionService
{
    private readonly AppDbContext _context;

    public PromotionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PromotionDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.Promotions.AsQueryable();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        var rows = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PromotionDto?> GetAsync(int id, CancellationToken ct)
    {
        var p = await _context.Promotions.FirstOrDefaultAsync(x => x.Id == id, ct);
        return p == null ? null : ToDto(p);
    }

    public async Task<PromotionDto> CreateAsync(PromotionForCreateDto dto, CancellationToken ct)
    {
        await ValidateCodeUniqueAsync(dto.Code, excludeId: null, ct);
        ValidateShape(dto.Type, dto.Value);

        var promo = new Promotion
        {
            Name = dto.Name,
            Code = NormalizeCode(dto.Code),
            Type = dto.Type,
            Value = dto.Value,
            MinOrderAmount = dto.MinOrderAmount,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            MaxRedemptions = dto.MaxRedemptions,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync(ct);
        return ToDto(promo);
    }

    public async Task<PromotionDto> UpdateAsync(int id, PromotionForUpdateDto dto, CancellationToken ct)
    {
        var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_PROMO_NOT_FOUND", $"Promotion {id} introuvable.");
        await ValidateCodeUniqueAsync(dto.Code, excludeId: id, ct);
        ValidateShape(dto.Type, dto.Value);

        promo.Name = dto.Name;
        promo.Code = NormalizeCode(dto.Code);
        promo.Type = dto.Type;
        promo.Value = dto.Value;
        promo.MinOrderAmount = dto.MinOrderAmount;
        promo.StartsAt = dto.StartsAt;
        promo.EndsAt = dto.EndsAt;
        promo.MaxRedemptions = dto.MaxRedemptions;
        promo.IsActive = dto.IsActive;
        promo.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(promo);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainNotFoundException("ERR_PROMO_NOT_FOUND", $"Promotion {id} introuvable.");
        promo.IsActive = false;
        promo.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PromotionApplyResultDto?> EvaluateAsync(string? code, decimal subtotal, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = NormalizeCode(code)!;
            var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Code == normalized, ct)
                ?? throw new DomainException("ERR_PROMO_INVALID", "Code promotionnel invalide.");
            EnsureUsable(promo, subtotal, now); // throws with a specific reason
            return Result(promo, subtotal);
        }

        // Auto-apply: pick the best (largest discount) qualifying code-less promo.
        var candidates = await _context.Promotions
            .Where(p => p.IsActive && p.Code == null)
            .ToListAsync(ct);

        PromotionApplyResultDto? best = null;
        foreach (var p in candidates)
        {
            if (!IsUsable(p, subtotal, now)) continue;
            var r = Result(p, subtotal);
            if (best == null || r.DiscountAmount > best.DiscountAmount) best = r;
        }
        return best;
    }

    public async Task RecordRedemptionAsync(int promotionId, CancellationToken ct)
    {
        var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == promotionId, ct);
        if (promo == null) return;
        promo.RedemptionCount++;
        await _context.SaveChangesAsync(ct);
    }

    // ----- Rules --------------------------------------------------------------------

    private static bool IsUsable(Promotion p, decimal subtotal, DateTime now)
        => p.IsActive
           && (p.StartsAt == null || p.StartsAt <= now)
           && (p.EndsAt == null || p.EndsAt > now)
           && subtotal >= p.MinOrderAmount
           && (p.MaxRedemptions == null || p.RedemptionCount < p.MaxRedemptions);

    private static void EnsureUsable(Promotion p, decimal subtotal, DateTime now)
    {
        if (!p.IsActive) throw new DomainException("ERR_PROMO_INACTIVE", "Cette promotion n'est pas active.");
        if (p.StartsAt != null && p.StartsAt > now) throw new DomainException("ERR_PROMO_NOT_STARTED", "Cette promotion n'a pas encore commencé.");
        if (p.EndsAt != null && p.EndsAt <= now) throw new DomainException("ERR_PROMO_EXPIRED", "Cette promotion a expiré.");
        if (subtotal < p.MinOrderAmount)
            throw new DomainException("ERR_PROMO_MIN_NOT_MET",
                $"Un minimum de {p.MinOrderAmount} est requis pour cette promotion.");
        if (p.MaxRedemptions != null && p.RedemptionCount >= p.MaxRedemptions)
            throw new DomainException("ERR_PROMO_EXHAUSTED", "Cette promotion a atteint sa limite d'utilisation.");
    }

    private static PromotionApplyResultDto Result(Promotion p, decimal subtotal)
    {
        var discount = p.Type == PromotionType.Percentage
            ? Math.Round(subtotal * (p.Value / 100m), 2, MidpointRounding.AwayFromZero)
            : Math.Min(p.Value, subtotal); // a fixed discount never exceeds the subtotal
        return new PromotionApplyResultDto { PromotionId = p.Id, Name = p.Name, DiscountAmount = discount };
    }

    private static void ValidateShape(PromotionType type, decimal value)
    {
        if (type == PromotionType.Percentage && (value <= 0 || value > 100))
            throw new DomainException("ERR_PROMO_PERCENT_RANGE", "Le pourcentage doit être entre 0 et 100.");
        if (type == PromotionType.FixedAmount && value <= 0)
            throw new DomainException("ERR_PROMO_AMOUNT_RANGE", "Le montant doit être positif.");
    }

    private async Task ValidateCodeUniqueAsync(string? code, int? excludeId, CancellationToken ct)
    {
        var normalized = NormalizeCode(code);
        if (normalized == null) return;
        var clash = await _context.Promotions
            .AnyAsync(p => p.Code == normalized && (excludeId == null || p.Id != excludeId), ct);
        if (clash) throw new DomainConflictException("ERR_PROMO_CODE_TAKEN", $"Le code '{normalized}' est déjà utilisé.");
    }

    private static string? NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static PromotionDto ToDto(Promotion p) => new()
    {
        Id = p.Id, Name = p.Name, Code = p.Code, Type = p.Type.ToString(), Value = p.Value,
        MinOrderAmount = p.MinOrderAmount, StartsAt = p.StartsAt, EndsAt = p.EndsAt,
        MaxRedemptions = p.MaxRedemptions, RedemptionCount = p.RedemptionCount, IsActive = p.IsActive,
    };
}
