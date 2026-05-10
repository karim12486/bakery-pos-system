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

        var user = await _context.Users
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

    public async Task<IReadOnlyList<string>?> GetActiveUsernamesAsync(CancellationToken ct)
    {
        var allowed = _config.GetValue<bool?>("Auth:AllowUsernameListing") ?? true;
        if (!allowed) return null;

        return await _context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .Select(u => u.Username)
            .ToListAsync(ct);
    }
}
