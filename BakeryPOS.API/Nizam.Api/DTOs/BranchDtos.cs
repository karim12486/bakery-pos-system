using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

public class BranchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public string? Currency { get; set; }
    public bool IsActive { get; set; }
}

public class BranchSelectDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }
}

/// <summary>Returned by <c>POST /api/branches/select</c> — a fresh token with branch_id baked in.</summary>
public class BranchSelectResultDto
{
    public string Token { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
}
