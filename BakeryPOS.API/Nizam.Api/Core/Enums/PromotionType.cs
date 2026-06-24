namespace Nizam.Api.Core.Enums;

/// <summary>How a <see cref="Entities.Promotion"/> reduces an order total.</summary>
public enum PromotionType
{
    /// <summary>A percentage off the order subtotal (Value = percent, 0–100).</summary>
    Percentage,

    /// <summary>A fixed amount off the order subtotal (Value = currency amount).</summary>
    FixedAmount
}
