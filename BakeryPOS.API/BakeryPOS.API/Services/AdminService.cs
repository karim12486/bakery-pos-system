using AutoMapper;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.DTOs.Shared;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IAdminService
{
    Task<PagedResponse<UserDetailDto>> ListUsersAsync(string? search, PaginationParams pagination, CancellationToken ct);
    Task<UserDetailDto?> GetUserAsync(int id, CancellationToken ct);
    Task CreateUserAsync(UserForCreationDto dto, CancellationToken ct);
    Task UpdateUserAsync(int id, UserForUpdateDto dto, string currentUsername, CancellationToken ct);
    Task DeactivateUserAsync(int id, string currentUsername, CancellationToken ct);
    Task ResetPasswordAsync(int id, ResetPasswordDto dto, CancellationToken ct);
    IReadOnlyList<PermissionDto> ListPermissions();

    Task<IReadOnlyList<UserBranchRoleDto>> ListUserBranchRolesAsync(int userId, CancellationToken ct);
    Task<UserBranchRoleDto> AssignBranchRoleAsync(UserBranchRoleAssignDto dto, CancellationToken ct);
    Task RevokeBranchRoleAsync(int id, CancellationToken ct);
}

public sealed class AdminService : IAdminService
{
    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly IMapper _mapper;

    public AdminService(AppDbContext context, IPasswordService passwordService, IMapper mapper)
    {
        _context = context;
        _passwordService = passwordService;
        _mapper = mapper;
    }

    public async Task<PagedResponse<UserDetailDto>> ListUsersAsync(string? search, PaginationParams pagination, CancellationToken ct)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(search) || u.Username.ToLower().Contains(search));
        }

        var totalRecords = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<UserDetailDto>(_mapper.Map<IEnumerable<UserDetailDto>>(users),
            pagination.PageNumber, pagination.PageSize, totalRecords);
    }

    public async Task<UserDetailDto?> GetUserAsync(int id, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object?[] { id }, ct);
        return user == null ? null : _mapper.Map<UserDetailDto>(user);
    }

    public async Task CreateUserAsync(UserForCreationDto dto, CancellationToken ct)
    {
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower(), ct))
            throw new DomainConflictException("ERR_USERNAME_TAKEN", "Ce nom d'utilisateur est déjà pris.");

        var newUser = _mapper.Map<User>(dto);
        newUser.PasswordHash = _passwordService.HashPassword(dto.Password);
        newUser.IsActive = true;
        newUser.CreatedAt = DateTime.UtcNow;

        await _context.Users.AddAsync(newUser, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateUserAsync(int id, UserForUpdateDto dto, string currentUsername, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.");

        var isSelf = user.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase);
        var newPermissions = (UserPermissions)dto.Permissions;

        if (isSelf)
        {
            if (!dto.IsActive)
                throw new DomainException("ERR_SELF_DEACTIVATE", "Vous ne pouvez pas désactiver votre propre compte.");
            if (!newPermissions.HasFlag(UserPermissions.ManageUsers))
                throw new DomainException("ERR_SELF_REVOKE_ADMIN",
                    "Vous ne pouvez pas retirer votre propre permission de gestion des utilisateurs.");
        }

        // Last-admin guard: if this user currently has ManageUsers and the change removes it
        // (or deactivates them), ensure at least one OTHER active user keeps ManageUsers.
        var losingManageUsers = user.Permissions.HasFlag(UserPermissions.ManageUsers)
            && (!newPermissions.HasFlag(UserPermissions.ManageUsers) || !dto.IsActive);

        if (losingManageUsers)
        {
            var otherManagers = await _context.Users.CountAsync(u =>
                u.Id != user.Id
                && u.IsActive
                && (u.Permissions & UserPermissions.ManageUsers) == UserPermissions.ManageUsers, ct);

            if (otherManagers == 0)
                throw new DomainException("ERR_LAST_MANAGER",
                    "Au moins un utilisateur actif doit conserver la permission de gestion des utilisateurs.");
        }

        _mapper.Map(dto, user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeactivateUserAsync(int id, string currentUsername, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.");

        if (user.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("ERR_SELF_DEACTIVATE", "Vous ne pouvez pas désactiver votre propre compte.");

        user.IsActive = false;
        await _context.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordDto dto, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.");

        user.PasswordHash = _passwordService.HashPassword(dto.NewPassword);
        await _context.SaveChangesAsync(ct);
    }

    public IReadOnlyList<PermissionDto> ListPermissions() =>
        Enum.GetValues(typeof(UserPermissions))
            .Cast<UserPermissions>()
            .Where(p => p != UserPermissions.None && p != UserPermissions.Admin)
            .Select(p => new PermissionDto { Name = p.ToString(), Value = (int)p })
            .ToList();

    public async Task<IReadOnlyList<UserBranchRoleDto>> ListUserBranchRolesAsync(int userId, CancellationToken ct)
    {
        return await _context.UserBranchRoles
            .Include(r => r.User)
            .Include(r => r.Branch)
            .Where(r => r.UserId == userId)
            .Select(r => new UserBranchRoleDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserFullName = r.User!.FullName,
                BranchId = r.BranchId,
                BranchName = r.Branch!.Name,
                Permissions = (int)r.Permissions
            })
            .ToListAsync(ct);
    }

    public async Task<UserBranchRoleDto> AssignBranchRoleAsync(UserBranchRoleAssignDto dto, CancellationToken ct)
    {
        var user = await _context.Users.FindAsync(new object?[] { dto.UserId }, ct)
            ?? throw new DomainNotFoundException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.");
        var branch = await _context.Branches.FindAsync(new object?[] { dto.BranchId }, ct)
            ?? throw new DomainNotFoundException("ERR_BRANCH_NOT_FOUND", "Branche introuvable.");

        // Upsert: one row per (user, branch). Re-assignment replaces the perm set.
        var existing = await _context.UserBranchRoles
            .FirstOrDefaultAsync(r => r.UserId == dto.UserId && r.BranchId == dto.BranchId, ct);

        UserBranchRole role;
        if (existing != null)
        {
            existing.Permissions = dto.Permissions;
            role = existing;
        }
        else
        {
            role = new UserBranchRole
            {
                UserId = dto.UserId,
                BranchId = dto.BranchId,
                Permissions = dto.Permissions
                // TenantId auto-stamped by AppDbContext
            };
            _context.UserBranchRoles.Add(role);
        }

        await _context.SaveChangesAsync(ct);

        return new UserBranchRoleDto
        {
            Id = role.Id,
            UserId = role.UserId,
            UserFullName = user.FullName,
            BranchId = role.BranchId,
            BranchName = branch.Name,
            Permissions = (int)role.Permissions
        };
    }

    public async Task RevokeBranchRoleAsync(int id, CancellationToken ct)
    {
        var role = await _context.UserBranchRoles.FindAsync(new object?[] { id }, ct)
            ?? throw new DomainNotFoundException("ERR_BRANCH_ROLE_NOT_FOUND", "Affectation introuvable.");
        _context.UserBranchRoles.Remove(role);
        await _context.SaveChangesAsync(ct);
    }
}
