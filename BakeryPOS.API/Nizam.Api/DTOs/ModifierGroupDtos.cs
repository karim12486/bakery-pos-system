using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

/// <summary>Read DTO — a modifier group with its modifiers.</summary>
public class ModifierGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<ModifierDto> Modifiers { get; set; } = Array.Empty<ModifierDto>();
}

/// <summary>Read DTO — a single modifier (option) within a group.</summary>
public class ModifierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Create DTO — payload for <c>POST /api/modifier-groups</c>.</summary>
public class ModifierGroupForCreateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    /// <summary>Minimum number of modifiers that MUST be selected from this group. 0 = optional.</summary>
    [Range(0, 50)]
    public int MinSelect { get; set; }

    /// <summary>Maximum number of modifiers that MAY be selected. 1 = single-choice, greater = multi-select.</summary>
    [Range(1, 50)]
    public int MaxSelect { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    /// <summary>Optional initial set of modifiers. Can be added later via <c>POST /api/modifier-groups/{id}/modifiers</c>.</summary>
    public IReadOnlyList<ModifierForCreateDto> Modifiers { get; set; } = Array.Empty<ModifierForCreateDto>();
}

/// <summary>Update DTO — payload for <c>PUT /api/modifier-groups/{id}</c>.</summary>
public class ModifierGroupForUpdateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    [Range(0, 50)]
    public int MinSelect { get; set; }

    [Range(1, 50)]
    public int MaxSelect { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Create DTO — payload for adding a modifier to an existing group.</summary>
public class ModifierForCreateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Can be zero or negative.</summary>
    [Range(typeof(decimal), "-100000", "100000")]
    public decimal PriceDelta { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public bool IsDefault { get; set; }
}

/// <summary>Update DTO — payload for <c>PUT /api/modifier-groups/{groupId}/modifiers/{id}</c>.</summary>
public class ModifierForUpdateDto
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "-100000", "100000")]
    public decimal PriceDelta { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Payload for attaching a modifier group to a product.</summary>
public class ProductModifierGroupAttachDto
{
    [Required, Range(1, int.MaxValue)]
    public int ModifierGroupId { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }
}
