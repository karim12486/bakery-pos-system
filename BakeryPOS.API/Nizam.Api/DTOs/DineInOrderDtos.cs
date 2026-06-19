using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

/// <summary>A dine-in order with its current line items and running totals.</summary>
public class DineInOrderDto
{
    public int OrderId { get; set; }
    public int? TableId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public IReadOnlyList<DineInOrderItemDto> Items { get; set; } = Array.Empty<DineInOrderItemDto>();
}

public class DineInOrderItemDto
{
    public int OrderItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? KitchenStationId { get; set; }
    public IReadOnlyList<string> Modifiers { get; set; } = Array.Empty<string>();
}

/// <summary>Payload to append items to an open dine-in order.</summary>
public class AddOrderItemsDto
{
    [Required, MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<SaleDetailForCreateDto> Items { get; set; } = new();
}
