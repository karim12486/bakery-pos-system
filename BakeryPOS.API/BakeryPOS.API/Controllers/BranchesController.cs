using System.Security.Claims;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeryPOS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branches;

    public BranchesController(IBranchService branches)
    {
        _branches = branches;
    }

    /// <summary>
    /// Lists branches the current user can work at — Admins see all; other users see branches
    /// they have a UserBranchRole row for. Powers the "Which branch are you at today?" picker
    /// from the Figma.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<BranchDto>>> ListMine(CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        return Ok(await _branches.ListAccessibleAsync(username, ct));
    }

    /// <summary>
    /// Pick a branch — mints a NEW JWT with the <c>branch_id</c> claim added. The client
    /// REPLACES its current token with this one for the rest of the session. From then on,
    /// HasPermissionFilter unions the user's per-branch grants for THAT branch.
    /// </summary>
    [HttpPost("select")]
    public async Task<ActionResult<BranchSelectResultDto>> Select(BranchSelectDto dto, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
        return Ok(await _branches.SelectAsync(dto.BranchId, username, ct));
    }

    /// <summary>Admin view of all branches in the tenant.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BranchDto>>> List(CancellationToken ct)
        => Ok(await _branches.ListAllAsync(ct));
}
