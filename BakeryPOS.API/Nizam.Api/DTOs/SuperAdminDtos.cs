using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

public class SuperAdminLoginDto
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class SuperAdminTokenDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>Platform-level view of a tenant.</summary>
public class TenantSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int BranchCount { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenantDetailDto
{
    public TenantSummaryDto Tenant { get; set; } = new();
    public IReadOnlyList<TenantFeatureOverrideDto> FeatureOverrides { get; set; } = Array.Empty<TenantFeatureOverrideDto>();
}

public class TenantFeatureOverrideDto
{
    public string FeatureKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
}

public class ChangePlanDto
{
    [Required, StringLength(40, MinimumLength = 1)]
    public string PlanCode { get; set; } = string.Empty;
}

public class SetFeatureOverrideDto
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string FeatureKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }
}
