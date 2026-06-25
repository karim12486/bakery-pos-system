using System.ComponentModel.DataAnnotations.Schema;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// Per-tenant loyalty program configuration. One row per tenant. Controls how points are
/// earned on spend and what they're worth on redemption.
/// </summary>
public class LoyaltyProgram
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Points earned per 1 currency unit spent (e.g. 0.1 = 1 point per 10 EGP).</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal EarnPointsPerCurrency { get; set; } = 0.1m;

    /// <summary>Currency value of 1 point on redemption (e.g. 0.5 = 100 points → 50 EGP).</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal RedeemCurrencyPerPoint { get; set; } = 0.5m;

    /// <summary>Minimum points required to redeem at all.</summary>
    public int MinRedeemPoints { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A customer's points wallet. One per (tenant, customer).</summary>
public class LoyaltyAccount
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Current redeemable points balance. Never negative.</summary>
    public int PointsBalance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LoyaltyTransaction> Transactions { get; set; } = new List<LoyaltyTransaction>();
}

/// <summary>An immutable ledger entry — every point movement, for auditability.</summary>
public class LoyaltyTransaction
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int LoyaltyAccountId { get; set; }
    public LoyaltyAccount? Account { get; set; }

    public LoyaltyTransactionType Type { get; set; }

    /// <summary>Signed delta: positive for Earn, negative for Redeem, either for Adjust.</summary>
    public int Points { get; set; }

    /// <summary>Resulting balance after this entry (denormalised for statement rendering).</summary>
    public int BalanceAfter { get; set; }

    public string? Reason { get; set; }

    /// <summary>The sale that drove this entry, when applicable.</summary>
    public int? RelatedSaleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
