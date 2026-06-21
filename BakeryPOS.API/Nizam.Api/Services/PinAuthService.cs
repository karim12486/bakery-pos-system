using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IPinAuthService
{
    /// <summary>Sets/changes a user's PIN. Enforces 4–6 digits + per-tenant uniqueness.
    /// Runs in the admin's tenant scope.</summary>
    Task SetPinAsync(int userId, string pin, CancellationToken ct);

    /// <summary>Quick login by tenant slug + PIN. Cross-tenant lookup (pre-auth). Mints a
    /// normal JWT on success; generic error on any failure (no tenant/PIN enumeration).</summary>
    Task<UserDto> PinLoginAsync(PinLoginDto dto, CancellationToken ct);

    /// <summary>Manager-override primitive: returns the user in the CURRENT tenant whose PIN
    /// matches AND who holds <paramref name="requiredPermission"/>. Throws 403 otherwise.
    /// Used by flows that need an inline supervisor approval (void/comp, etc.).</summary>
    Task<User> ValidateManagerPinAsync(string pin, UserPermissions requiredPermission, CancellationToken ct);
}

public sealed class PinAuthService : IPinAuthService
{
    // Spend BCrypt time on the no-match branch so PIN-login latency doesn't reveal whether a
    // matching PIN exists. Computed once at process start.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;

    public PinAuthService(AppDbContext context, IPasswordService passwordService, ITokenService tokenService)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    public async Task SetPinAsync(int userId, string pin, CancellationToken ct)
    {
        if (!IsValidPinFormat(pin))
            throw new DomainException("ERR_INVALID_PIN", "PIN must be 4 to 6 digits.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new DomainNotFoundException("ERR_USER_NOT_FOUND", "Utilisateur introuvable.");

        // Per-tenant uniqueness: no two users in a tenant may share a PIN, so PIN-login is
        // deterministic. Check against every OTHER active user with a PIN in this tenant.
        var others = await _context.Users
            .Where(u => u.Id != userId && u.IsActive && u.PinHash != null)
            .Select(u => u.PinHash!)
            .ToListAsync(ct);
        if (others.Any(hash => _passwordService.VerifyPassword(pin, hash)))
            throw new DomainConflictException("ERR_PIN_TAKEN",
                "This PIN is already in use. Choose a different one.");

        user.PinHash = _passwordService.HashPassword(pin);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<UserDto> PinLoginAsync(PinLoginDto dto, CancellationToken ct)
    {
        const string genericError = "Échec de la connexion par code PIN.";

        // Pre-auth: no tenant in scope. Resolve the tenant by slug (unfiltered), then look only
        // at that tenant's active PIN-enabled users.
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == dto.TenantSlug.ToLower(), ct);

        if (tenant == null)
        {
            _passwordService.VerifyPassword(dto.Pin, DummyHash); // constant-time-ish
            throw new DomainException("ERR_PIN_LOGIN_FAILED", genericError, StatusCodes.Status401Unauthorized);
        }

        var candidates = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenant.Id && u.IsActive && u.PinHash != null)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(u => _passwordService.VerifyPassword(dto.Pin, u.PinHash!));
        if (match == null)
        {
            _passwordService.VerifyPassword(dto.Pin, DummyHash);
            throw new DomainException("ERR_PIN_LOGIN_FAILED", genericError, StatusCodes.Status401Unauthorized);
        }

        return ToUserDto(match);
    }

    public async Task<User> ValidateManagerPinAsync(string pin, UserPermissions requiredPermission, CancellationToken ct)
    {
        // Tenant IS in scope (the caller is authenticated) — the closed filter restricts to the
        // current tenant automatically.
        var candidates = await _context.Users
            .Where(u => u.IsActive && u.PinHash != null)
            .ToListAsync(ct);

        var manager = candidates.FirstOrDefault(u => _passwordService.VerifyPassword(pin, u.PinHash!));
        if (manager == null)
        {
            _passwordService.VerifyPassword(pin, DummyHash);
            throw new DomainException("ERR_OVERRIDE_PIN_INVALID",
                "Code PIN superviseur invalide.", StatusCodes.Status403Forbidden);
        }

        if (!manager.Permissions.HasFlag(requiredPermission))
            throw new DomainException("ERR_OVERRIDE_FORBIDDEN",
                "Ce superviseur n'a pas l'autorisation requise.", StatusCodes.Status403Forbidden);

        return manager;
    }

    private static bool IsValidPinFormat(string pin)
        => !string.IsNullOrEmpty(pin) && pin.Length is >= 4 and <= 6 && pin.All(char.IsDigit);

    private UserDto ToUserDto(User user)
    {
        var role = !string.IsNullOrEmpty(user.Role)
            ? user.Role
            : (user.Permissions.HasFlag(UserPermissions.Admin) ? "Admin" : "Cashier");

        return new UserDto
        {
            Username = user.Username,
            FullName = user.FullName,
            Token = _tokenService.CreateToken(user),
            Role = role,
            Permissions = (int)user.Permissions,
            ImageUrl = user.ImageUrl,
        };
    }
}
