using System.ComponentModel.DataAnnotations.Schema;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// One printed bill for an <see cref="Order"/>. An order produces 1..N checks when guests
/// split the bill. Each check carries its own money math (subtotal + service charge + tax =
/// total) and settles independently; the order closes once every check is paid.
///
/// <para><b>Money model</b>: <see cref="Subtotal"/> is the food/drink portion this check is
/// responsible for (sum of its items for ByItem splits, or a share of the order total for
/// Equal/Percentage). <see cref="ServiceChargeAmount"/> = subtotal × 12% unless
/// <see cref="ServiceChargeOptedOut"/>. <see cref="TaxAmount"/> = subtotal × branch tax rate.
/// <see cref="Total"/> = subtotal + service charge + tax.</para>
/// </summary>
public class Check
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Human label for the bill, e.g. "Check 1".</summary>
    public string Label { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal ServiceChargeAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Total { get; set; }

    /// <summary>When true, this check waives the automatic service charge.</summary>
    public bool ServiceChargeOptedOut { get; set; }

    public CheckStatus Status { get; set; } = CheckStatus.Open;

    /// <summary>Set when paid.</summary>
    public PaymentType? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CheckItem> Items { get; set; } = new List<CheckItem>();
}

/// <summary>
/// Assigns one <see cref="OrderItem"/> to a <see cref="Check"/> (ByItem / Single splits).
/// Equal and Percentage splits don't create these — they divide money, not items. An order
/// item belongs to at most one check.
/// </summary>
public class CheckItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int CheckId { get; set; }
    public Check? Check { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
}
