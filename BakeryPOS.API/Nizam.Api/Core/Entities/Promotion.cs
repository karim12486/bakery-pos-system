using System.ComponentModel.DataAnnotations.Schema;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// A rule-driven discount. Either auto-applies (no code) or is redeemed by a coupon
/// <see cref="Code"/>. Qualifies on a minimum order, an optional time window, and an optional
/// redemption cap. v1 discounts the order subtotal (Percentage or FixedAmount).
/// </summary>
public class Promotion
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Coupon code (case-insensitive, unique per tenant when set). Null = auto-apply.</summary>
    public string? Code { get; set; }

    public PromotionType Type { get; set; }

    /// <summary>Percent (0–100) for <see cref="PromotionType.Percentage"/>, or a currency amount
    /// for <see cref="PromotionType.FixedAmount"/>.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Value { get; set; }

    /// <summary>Minimum order subtotal required to qualify. 0 = no minimum.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal MinOrderAmount { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    /// <summary>Optional total-redemptions cap across all customers. Null = unlimited.</summary>
    public int? MaxRedemptions { get; set; }

    /// <summary>How many times this promo has been redeemed.</summary>
    public int RedemptionCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
