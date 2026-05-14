using System.ComponentModel.DataAnnotations;
using BakeryPOS.API.Core.Enums;

namespace BakeryPOS.API.DTOs;

public class UserBranchRoleDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int Permissions { get; set; }
}

public class UserBranchRoleAssignDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    [Required]
    public UserPermissions Permissions { get; set; }
}
