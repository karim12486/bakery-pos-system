using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IBranchService
{
    /// <summary>Branches the current user can work at — union of: every branch (if they have
    /// any tenant-level Admin perm), plus any branches they have a UserBranchRole row for.</summary>
    Task<IReadOnlyList<BranchDto>> ListAccessibleAsync(string username, CancellationToken ct);

    /// <summary>Validates that the user can access the branch and mints a NEW JWT with branch_id
    /// embedded. Returned to the client to replace the current token.</summary>
    Task<BranchSelectResultDto> SelectAsync(int branchId, string username, CancellationToken ct);

    Task<IReadOnlyList<BranchDto>> ListAllAsync(CancellationToken ct);
}

public sealed class BranchService : IBranchService
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;

    public BranchService(AppDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<IReadOnlyList<BranchDto>> ListAccessibleAsync(string username, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (user == null) return Array.Empty<BranchDto>();

        // Tenant-level Admins (or any user with a tenant-wide perm) see ALL branches in their tenant.
        // The closed filter already scopes to the user's tenant.
        var allBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync(ct);

        // If the user has Admin tenant-wide, everything is accessible.
        if (user.Permissions.HasFlag(UserPermissions.Admin))
        {
            return allBranches.Select(ToDto).ToList();
        }

        // Otherwise: branches the user has a UserBranchRole row for.
        var assignedBranchIds = await _context.UserBranchRoles
            .Where(r => r.UserId == user.Id)
            .Select(r => r.BranchId)
            .ToListAsync(ct);

        return allBranches.Where(b => assignedBranchIds.Contains(b.Id)).Select(ToDto).ToList();
    }

    public async Task<BranchSelectResultDto> SelectAsync(int branchId, string username, CancellationToken ct)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new DomainException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.", StatusCodes.Status401Unauthorized);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", "Branche introuvable.");

        if (!branch.IsActive)
            throw new DomainConflictException("ERR_BRANCH_INACTIVE", "Cette branche est désactivée.");

        // Authorisation: tenant-Admin always passes. Otherwise the user must have a
        // UserBranchRole row for this specific branch.
        var hasAccess = user.Permissions.HasFlag(UserPermissions.Admin)
            || await _context.UserBranchRoles.AnyAsync(r => r.UserId == user.Id && r.BranchId == branchId, ct);

        if (!hasAccess)
            throw new DomainException("ERR_BRANCH_FORBIDDEN",
                "Vous n'avez pas accès à cette branche.", StatusCodes.Status403Forbidden);

        var token = _tokenService.CreateToken(user, branchId);

        return new BranchSelectResultDto
        {
            Token = token,
            BranchId = branch.Id,
            BranchName = branch.Name
        };
    }

    public async Task<IReadOnlyList<BranchDto>> ListAllAsync(CancellationToken ct)
    {
        var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync(ct);
        return branches.Select(ToDto).ToList();
    }

    private static BranchDto ToDto(Branch b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Address = b.Address,
        Timezone = b.Timezone,
        TaxRate = b.TaxRate,
        Currency = b.Currency,
        IsActive = b.IsActive
    };
}
