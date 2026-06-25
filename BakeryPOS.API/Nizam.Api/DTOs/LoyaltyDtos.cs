using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

public class LoyaltyProgramDto
{
    public decimal EarnPointsPerCurrency { get; set; }
    public decimal RedeemCurrencyPerPoint { get; set; }
    public int MinRedeemPoints { get; set; }
    public bool IsActive { get; set; }
}

public class LoyaltyProgramUpdateDto
{
    [Range(0, 1000)] public decimal EarnPointsPerCurrency { get; set; }
    [Range(0, 1000)] public decimal RedeemCurrencyPerPoint { get; set; }
    [Range(0, int.MaxValue)] public int MinRedeemPoints { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LoyaltyAccountDto
{
    public int CustomerId { get; set; }
    public int PointsBalance { get; set; }
    public IReadOnlyList<LoyaltyTransactionDto> RecentTransactions { get; set; } = Array.Empty<LoyaltyTransactionDto>();
}

public class LoyaltyTransactionDto
{
    public string Type { get; set; } = string.Empty;
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public string? Reason { get; set; }
    public int? RelatedSaleId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Manual staff adjustment of a customer's points (signed).</summary>
public class LoyaltyAdjustDto
{
    [Required] public int CustomerId { get; set; }

    /// <summary>Signed points delta (positive to grant, negative to deduct).</summary>
    [Range(-1_000_000, 1_000_000)]
    public int Points { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Result of a redemption — points spent + the currency value applied.</summary>
public class LoyaltyRedeemResultDto
{
    public int PointsRedeemed { get; set; }
    public decimal CurrencyValue { get; set; }
    public int RemainingBalance { get; set; }
}
