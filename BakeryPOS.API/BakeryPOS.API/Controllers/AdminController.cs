using System.Security.Claims;
using BakeryPOS.API.Core.Attributes;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[HasPermission(UserPermissions.ManageUsers)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet("users")]
    public async Task<ActionResult<PagedResponse<UserDetailDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] PaginationParams pagination,
        CancellationToken ct)
        => Ok(await _admin.ListUsersAsync(search, pagination, ct));

    [HttpGet("users/{id:int}")]
    public async Task<ActionResult<UserDetailDto>> GetUser(int id, CancellationToken ct)
    {
        var user = await _admin.GetUserAsync(id, ct);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(UserForCreationDto dto, CancellationToken ct)
    {
        await _admin.CreateUserAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, "Utilisateur créé avec succès.");
    }

    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, UserForUpdateDto dto, CancellationToken ct)
    {
        var currentUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        await _admin.UpdateUserAsync(id, dto, currentUsername, ct);
        return NoContent();
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    {
        var currentUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        await _admin.DeactivateUserAsync(id, currentUsername, ct);
        return NoContent();
    }

    [HttpPost("users/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto, CancellationToken ct)
    {
        await _admin.ResetPasswordAsync(id, dto, ct);
        return Ok("Mot de passe mis à jour avec succès.");
    }

    [HttpGet("permissions")]
    public IActionResult GetPermissions() => Ok(_admin.ListPermissions());

    /// <summary>Lists branch-scoped role assignments for a user (union'd with their tenant-level perms).</summary>
    [HttpGet("users/{userId:int}/branch-roles")]
    public async Task<ActionResult<IEnumerable<UserBranchRoleDto>>> ListUserBranchRoles(int userId, CancellationToken ct)
        => Ok(await _admin.ListUserBranchRolesAsync(userId, ct));

    /// <summary>Grants (or replaces) a per-branch permission set for a user. Idempotent — calling with
    /// the same (userId, branchId) updates the permissions in place.</summary>
    [HttpPost("branch-roles")]
    public async Task<ActionResult<UserBranchRoleDto>> AssignBranchRole(UserBranchRoleAssignDto dto, CancellationToken ct)
        => Ok(await _admin.AssignBranchRoleAsync(dto, ct));

    [HttpDelete("branch-roles/{id:int}")]
    public async Task<IActionResult> RevokeBranchRole(int id, CancellationToken ct)
    {
        await _admin.RevokeBranchRoleAsync(id, ct);
        return NoContent();
    }
}
