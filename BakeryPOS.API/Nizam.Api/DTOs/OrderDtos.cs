using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

/// <summary>
/// Create or update a parked cart (= Order with Status=Open). Items replace the order's
/// current items in their entirety — simplest semantic for a draft cart, no per-line diffs.
/// </summary>
public class ParkedCartDto
{
    [StringLength(100)]
    public string? Name { get; set; }

    public int? CustomerId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Le panier doit contenir au moins un article.")]
    public ICollection<SaleDetailForCreateDto> Items { get; set; } = new List<SaleDetailForCreateDto>();
}

public class ParkedCartItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class ParkedCartDetailDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime OpenedAt { get; set; }
    public decimal Subtotal { get; set; }
    public ICollection<ParkedCartItemDto> Items { get; set; } = new List<ParkedCartItemDto>();
}
