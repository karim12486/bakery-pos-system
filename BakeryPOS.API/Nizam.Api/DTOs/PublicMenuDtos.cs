namespace Nizam.Api.DTOs;

/// <summary>The public, anonymous menu served to QR-code diners. No prices-by-cost, no stock,
/// no internal ids beyond what's needed to place an order — just what a guest needs to choose.</summary>
public class PublicMenuDto
{
    public string TenantName { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public IReadOnlyList<PublicMenuCategoryDto> Categories { get; set; } = Array.Empty<PublicMenuCategoryDto>();
}

public class PublicMenuCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<PublicMenuProductDto> Products { get; set; } = Array.Empty<PublicMenuProductDto>();
}

public class PublicMenuProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public IReadOnlyList<PublicMenuModifierGroupDto> ModifierGroups { get; set; } = Array.Empty<PublicMenuModifierGroupDto>();
}

public class PublicMenuModifierGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public IReadOnlyList<PublicMenuModifierDto> Modifiers { get; set; } = Array.Empty<PublicMenuModifierDto>();
}

public class PublicMenuModifierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}
