using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ITenantSignupService
{
    Task<TenantSignupResultDto> SignupAsync(TenantSignupDto dto, CancellationToken ct);
}

public sealed class TenantSignupService : ITenantSignupService
{
    private readonly AppDbContext _context;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;

    public TenantSignupService(AppDbContext context, IPasswordService passwords, ITokenService tokens)
    {
        _context = context;
        _passwords = passwords;
        _tokens = tokens;
    }

    public async Task<TenantSignupResultDto> SignupAsync(TenantSignupDto dto, CancellationToken ct)
    {
        // Bypass the global tenant filter for everything we read here — signup runs without
        // any tenant in scope (the request is anonymous).
        var slug = dto.Slug.Trim().ToLowerInvariant();
        var slugTaken = await _context.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug, ct);
        if (slugTaken)
            throw new DomainConflictException("ERR_TENANT_SLUG_TAKEN",
                $"L'identifiant '{slug}' est déjà utilisé. Veuillez en choisir un autre.");

        // Username must be globally unique today (since the User.Username index is
        // tenant-agnostic). Once tenancy is fully isolated, this can become per-tenant.
        var username = dto.AdminUsername.Trim();
        var usernameTaken = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);
        if (usernameTaken)
            throw new DomainConflictException("ERR_USERNAME_TAKEN",
                "Ce nom d'utilisateur est déjà pris.");

        // All-or-nothing — tenant, branch, admin must land together or not at all.
        using var tx = await _context.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant
        {
            Name = dto.BusinessName.Trim(),
            Slug = slug,
            Plan = "trial",
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "EGP" : dto.Currency.Trim().ToUpperInvariant(),
            Locale = string.IsNullOrWhiteSpace(dto.Locale) ? "ar-EG" : dto.Locale.Trim(),
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(ct);

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = dto.BranchName.Trim(),
            Timezone = string.IsNullOrWhiteSpace(dto.BranchTimezone) ? "Africa/Cairo" : dto.BranchTimezone.Trim(),
            TaxRate = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Branches.Add(branch);

        var admin = new User
        {
            TenantId = tenant.Id,
            Username = username,
            PasswordHash = _passwords.HashPassword(dto.AdminPassword),
            FullName = dto.AdminFullName.Trim(),
            Permissions = UserPermissions.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Role = "Admin"
        };
        _context.Users.Add(admin);

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Mint the admin's JWT — no branch_id yet (they'll call Branch Select on next screen).
        var token = _tokens.CreateToken(admin);

        return new TenantSignupResultDto
        {
            TenantId = tenant.Id,
            Slug = tenant.Slug,
            BranchId = branch.Id,
            BranchName = branch.Name,
            Admin = new UserDto
            {
                Username = admin.Username,
                FullName = admin.FullName,
                Token = token,
                Role = "Admin",
                Permissions = (int)admin.Permissions,
                ImageUrl = admin.ImageUrl
            }
        };
    }
}
