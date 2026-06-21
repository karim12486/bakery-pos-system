using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs;

public class CheckDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public bool ServiceChargeOptedOut { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public IReadOnlyList<int> OrderItemIds { get; set; } = Array.Empty<int>();
}

/// <summary>Request to (re)split an order into checks. Existing UNPAID checks are replaced;
/// blocked if any check is already paid.</summary>
public class CreateChecksDto
{
    [Required]
    public SplitMode Mode { get; set; }

    /// <summary>Equal mode: number of checks (2–20).</summary>
    [Range(2, 20)]
    public int? SplitCount { get; set; }

    /// <summary>Percentage mode: percentages that sum to 100.</summary>
    public List<decimal>? Percentages { get; set; }

    /// <summary>ByItem mode: each check + the order-item ids it owns.</summary>
    public List<CheckAssignmentDto>? Assignments { get; set; }
}

public class CheckAssignmentDto
{
    [StringLength(60)]
    public string? Label { get; set; }

    [Required, MinLength(1)]
    public List<int> OrderItemIds { get; set; } = new();
}

public class SetServiceChargeDto
{
    public bool OptedOut { get; set; }
}

public class PayCheckDto
{
    [Required]
    public PaymentType PaymentMethod { get; set; }
}
