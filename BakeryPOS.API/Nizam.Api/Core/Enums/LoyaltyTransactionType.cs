namespace Nizam.Api.Core.Enums;

/// <summary>Why points moved on a <see cref="Entities.LoyaltyAccount"/>.</summary>
public enum LoyaltyTransactionType
{
    /// <summary>Points earned from a purchase (positive).</summary>
    Earn,

    /// <summary>Points spent for a discount (negative).</summary>
    Redeem,

    /// <summary>Manual staff adjustment (signed) — goodwill, correction, expiry.</summary>
    Adjust
}
