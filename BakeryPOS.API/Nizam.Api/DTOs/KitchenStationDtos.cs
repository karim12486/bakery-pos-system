using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

public class KitchenStationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class KitchenStationForCreateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }
}

public class KitchenStationForUpdateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Assigns (or clears) the kitchen station a category routes to.</summary>
public class CategoryRouteDto
{
    /// <summary>Target station id, or null to un-route the category.</summary>
    public int? KitchenStationId { get; set; }
}
