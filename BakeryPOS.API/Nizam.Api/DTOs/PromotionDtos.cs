using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs;

public class PromotionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public bool IsActive { get; set; }
}

public class PromotionForCreateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Code { get; set; }

    [Required]
    public PromotionType Type { get; set; }

    [Range(0, 1_000_000)]
    public decimal Value { get; set; }

    [Range(0, 1_000_000)]
    public decimal MinOrderAmount { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    [Range(1, int.MaxValue)]
    public int? MaxRedemptions { get; set; }
}

public class PromotionForUpdateDto : PromotionForCreateDto
{
    public bool IsActive { get; set; } = true;
}

/// <summary>Result of evaluating a promo against an order subtotal.</summary>
public class PromotionApplyResultDto
{
    public int PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
}
