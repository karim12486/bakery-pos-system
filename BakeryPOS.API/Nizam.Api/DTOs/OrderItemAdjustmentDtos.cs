using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

/// <summary>Void an order item. A manager PIN is required when the item has already been fired
/// to the kitchen (front-of-house can't silently drop a fired item without supervisor sign-off).</summary>
public class VoidItemDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Manager PIN — required to void an item that's past Pending.</summary>
    public string? ManagerPin { get; set; }
}

/// <summary>Comp an order item (make it free). Always requires a manager PIN.</summary>
public class CompItemDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{4,6}$", ErrorMessage = "Manager PIN must be 4 to 6 digits.")]
    public string ManagerPin { get; set; } = string.Empty;
}
