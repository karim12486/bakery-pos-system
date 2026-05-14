using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs;

/// <summary>Typed read of the well-known settings — what the frontend usually needs.</summary>
public class TenantSettingsDto
{
    public string? BusinessName { get; set; }
    public decimal? TaxRate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? ReceiptHeader { get; set; }
    public string? ReceiptFooter { get; set; }
    public string? DefaultLocale { get; set; }
    public string? BrandLogoUrl { get; set; }
}

public class SettingUpsertDto
{
    [Required]
    [StringLength(120)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Value { get; set; } = string.Empty;
}

public class AuditLogDto
{
    public int Id { get; set; }
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime At { get; set; }
}

public class AuditLogQuery
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public int? UserId { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
}
