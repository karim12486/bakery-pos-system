using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ILoyaltyService
{
    Task<LoyaltyProgramDto> GetProgramAsync(CancellationToken ct);
    Task<LoyaltyProgramDto> UpdateProgramAsync(LoyaltyProgramUpdateDto dto, CancellationToken ct);

    Task<LoyaltyAccountDto> GetAccountAsync(int customerId, CancellationToken ct);
    Task<LoyaltyAccountDto> AdjustAsync(int customerId, int points, string reason, CancellationToken ct);

    /// <summary>Earns points for a purchase (no-op if the program is inactive or amount ≤ 0).
    /// Called from the sale flow after commit.</summary>
    Task EarnFromSaleAsync(int customerId, decimal spendAmount, int saleId, CancellationToken ct);

    /// <summary>Validates + redeems points and returns the currency value to apply as a discount.
    /// Throws if the program is inactive, below the minimum, or the balance is insufficient.</summary>
    Task<LoyaltyRedeemResultDto> RedeemAsync(int customerId, int points, int? saleId, CancellationToken ct);
}

public sealed class LoyaltyService : ILoyaltyService
{
    private readonly AppDbContext _context;

    public LoyaltyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LoyaltyProgramDto> GetProgramAsync(CancellationToken ct)
        => ToDto(await GetOrCreateProgramAsync(ct));

    public async Task<LoyaltyProgramDto> UpdateProgramAsync(LoyaltyProgramUpdateDto dto, CancellationToken ct)
    {
        var program = await GetOrCreateProgramAsync(ct);
        program.EarnPointsPerCurrency = dto.EarnPointsPerCurrency;
        program.RedeemCurrencyPerPoint = dto.RedeemCurrencyPerPoint;
        program.MinRedeemPoints = dto.MinRedeemPoints;
        program.IsActive = dto.IsActive;
        program.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(program);
    }

    public async Task<LoyaltyAccountDto> GetAccountAsync(int customerId, CancellationToken ct)
    {
        var account = await _context.LoyaltyAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, ct);

        if (account == null)
        {
            if (!await _context.Customers.AnyAsync(c => c.Id == customerId, ct))
                throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");
            return new LoyaltyAccountDto { CustomerId = customerId, PointsBalance = 0 };
        }

        return new LoyaltyAccountDto
        {
            CustomerId = customerId,
            PointsBalance = account.PointsBalance,
            RecentTransactions = account.Transactions
                .OrderByDescending(t => t.CreatedAt).Take(20)
                .Select(ToTxDto).ToList(),
        };
    }

    public async Task<LoyaltyAccountDto> AdjustAsync(int customerId, int points, string reason, CancellationToken ct)
    {
        var account = await GetOrCreateAccountAsync(customerId, ct);
        if (account.PointsBalance + points < 0)
            throw new DomainConflictException("ERR_LOYALTY_NEGATIVE",
                "Adjustment would make the balance negative.");

        ApplyTransaction(account, LoyaltyTransactionType.Adjust, points, reason, null);
        await _context.SaveChangesAsync(ct);
        return await GetAccountAsync(customerId, ct);
    }

    public async Task EarnFromSaleAsync(int customerId, decimal spendAmount, int saleId, CancellationToken ct)
    {
        // Do NOT create a program here — a tenant who never configured loyalty (or lacks the
        // feature) must not silently accrue points. Earn only against an existing active program.
        var program = await _context.LoyaltyPrograms.FirstOrDefaultAsync(ct);
        if (program == null || !program.IsActive || spendAmount <= 0) return;

        var earned = (int)Math.Floor(spendAmount * program.EarnPointsPerCurrency);
        if (earned <= 0) return;

        var account = await GetOrCreateAccountAsync(customerId, ct);
        ApplyTransaction(account, LoyaltyTransactionType.Earn, earned, "Purchase", saleId);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<LoyaltyRedeemResultDto> RedeemAsync(int customerId, int points, int? saleId, CancellationToken ct)
    {
        if (points <= 0) throw new DomainException("ERR_LOYALTY_POINTS_INVALID", "Points must be positive.");

        var program = await GetOrCreateProgramAsync(ct);
        if (!program.IsActive) throw new DomainException("ERR_LOYALTY_INACTIVE", "Le programme de fidélité est inactif.");
        if (points < program.MinRedeemPoints)
            throw new DomainException("ERR_LOYALTY_MIN_REDEEM",
                $"Un minimum de {program.MinRedeemPoints} points est requis pour échanger.");

        var account = await GetOrCreateAccountAsync(customerId, ct);
        if (account.PointsBalance < points)
            throw new DomainConflictException("ERR_LOYALTY_INSUFFICIENT", "Solde de points insuffisant.");

        ApplyTransaction(account, LoyaltyTransactionType.Redeem, -points, "Redemption", saleId);
        await _context.SaveChangesAsync(ct);

        return new LoyaltyRedeemResultDto
        {
            PointsRedeemed = points,
            CurrencyValue = Math.Round(points * program.RedeemCurrencyPerPoint, 2, MidpointRounding.AwayFromZero),
            RemainingBalance = account.PointsBalance,
        };
    }

    // ----- Internals ----------------------------------------------------------------

    private static void ApplyTransaction(
        LoyaltyAccount account, LoyaltyTransactionType type, int points, string reason, int? saleId)
    {
        account.PointsBalance += points;
        account.UpdatedAt = DateTime.UtcNow;
        account.Transactions.Add(new LoyaltyTransaction
        {
            TenantId = account.TenantId,
            Type = type,
            Points = points,
            BalanceAfter = account.PointsBalance,
            Reason = reason,
            RelatedSaleId = saleId,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private async Task<LoyaltyProgram> GetOrCreateProgramAsync(CancellationToken ct)
    {
        var program = await _context.LoyaltyPrograms.FirstOrDefaultAsync(ct);
        if (program != null) return program;

        program = new LoyaltyProgram { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.LoyaltyPrograms.Add(program);
        await _context.SaveChangesAsync(ct);
        return program;
    }

    private async Task<LoyaltyAccount> GetOrCreateAccountAsync(int customerId, CancellationToken ct)
    {
        var account = await _context.LoyaltyAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, ct);
        if (account != null) return account;

        if (!await _context.Customers.AnyAsync(c => c.Id == customerId, ct))
            throw new DomainNotFoundException("ERR_CUSTOMER_NOT_FOUND", "Client introuvable.");

        account = new LoyaltyAccount { CustomerId = customerId, PointsBalance = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.LoyaltyAccounts.Add(account);
        await _context.SaveChangesAsync(ct);
        return account;
    }

    private static LoyaltyProgramDto ToDto(LoyaltyProgram p) => new()
    {
        EarnPointsPerCurrency = p.EarnPointsPerCurrency,
        RedeemCurrencyPerPoint = p.RedeemCurrencyPerPoint,
        MinRedeemPoints = p.MinRedeemPoints,
        IsActive = p.IsActive,
    };

    private static LoyaltyTransactionDto ToTxDto(LoyaltyTransaction t) => new()
    {
        Type = t.Type.ToString(), Points = t.Points, BalanceAfter = t.BalanceAfter,
        Reason = t.Reason, RelatedSaleId = t.RelatedSaleId, CreatedAt = t.CreatedAt,
    };
}
