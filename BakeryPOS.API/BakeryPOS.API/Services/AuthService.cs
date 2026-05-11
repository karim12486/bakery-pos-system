using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services;

public interface IAuthService
{
    Task<UserDto> LoginAsync(UserForLoginDto dto, CancellationToken ct);
    Task<IReadOnlyList<string>?> GetActiveUsernamesAsync(CancellationToken ct);

    /// <summary>
    /// Mints a fresh token for the username carried in the supplied JWT, IFF the signature
    /// is valid and the user is still active. Lifetime is irrelevant — by design, we want to
    /// refresh tokens that are mid-shift or recently expired. Lifetime is enforced by the
    /// AUTH middleware on protected endpoints; this endpoint deliberately accepts an expired
    /// token (within whatever window the validator's ClockSkew + ValidateLifetime=false allows).
    /// </summary>
    Task<UserDto> RefreshAsync(string username, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    // BCrypt hash of a random throwaway value, computed once at process start.
    // Used to spend verification time on the "user not found" branch so login latency
    // doesn't reveal whether a username exists. (Timing-attack mitigation.)
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext context, IPasswordService passwordService, ITokenService tokenService, IConfiguration config)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _config = config;
    }

    public async Task<UserDto> LoginAsync(UserForLoginDto dto, CancellationToken ct)
    {
        const string genericError = "Nom d'utilisateur ou mot de passe incorrect.";

        // IgnoreQueryFilters because the user's tenant isn't known yet — that's literally what
        // login is for. The token we mint below embeds tenant_id from the user we find, and from
        // then on the closed global filter scopes every query to that tenant.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower(), ct);

        // Always run BCrypt verify — same wall-clock cost whether the user exists or not.
        bool isPasswordValid;
        if (user != null)
        {
            isPasswordValid = _passwordService.VerifyPassword(dto.Password, user.PasswordHash);
        }
        else
        {
            _passwordService.VerifyPassword(dto.Password, DummyHash);
            isPasswordValid = false;
        }

        if (user == null || !user.IsActive || !isPasswordValid)
        {
            throw new DomainException("ERR_LOGIN_FAILED", genericError, StatusCodes.Status401Unauthorized);
        }

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
            ImageUrl = user.ImageUrl
        };
    }

    public async Task<UserDto> RefreshAsync(string username, CancellationToken ct)
    {
        // Cross-tenant lookup (we trust the signed JWT to tell us which user) — IgnoreQueryFilters
        // because the caller may have come in on an expired token that we deliberately allow.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Username == username, ct);

        if (user == null || !user.IsActive)
        {
            throw new DomainException("ERR_REFRESH_FAILED",
                "Impossible de renouveler la session. Veuillez vous reconnecter.",
                StatusCodes.Status401Unauthorized);
        }

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
            ImageUrl = user.ImageUrl
        };
    }

    public async Task<IReadOnlyList<string>?> GetActiveUsernamesAsync(CancellationToken ct)
    {
        var allowed = _config.GetValue<bool?>("Auth:AllowUsernameListing") ?? true;
        if (!allowed) return null;

        // Under the closed multi-tenant filter, this endpoint cannot work pre-login because no
        // tenant is in scope. The kiosk login picker needs to first identify the tenant (e.g. via
        // subdomain or a tenant-slug query param), then call a tenant-scoped variant. Returning
        // an empty list here is the safe interim behaviour — the legacy single-tenant kiosk flow
        // is broken under multi-tenancy and that's intentional until the new flow is wired up.
        // TODO[saas-launch]: replace with GetActiveUsernamesForTenantAsync(slug, ct).
        return await _context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .Select(u => u.Username)
            .ToListAsync(ct);
    }
}
