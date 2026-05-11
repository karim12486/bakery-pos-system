using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BakeryPOS.API.Common.Errors;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace BakeryPOS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IPasswordService _passwordService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService auth, IPasswordService passwordService, IConfiguration config)
    {
        _auth = auth;
        _passwordService = passwordService;
        _config = config;
    }

    [HttpGet("hash/{password}")]
    public ActionResult<string> GetHash(string password) =>
        Ok(_passwordService.HashPassword(password));

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<UserDto>> Login(UserForLoginDto dto, CancellationToken ct)
    {
        var user = await _auth.LoginAsync(dto, ct);
        return Ok(user);
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<string>>> GetUsernames(CancellationToken ct)
    {
        var usernames = await _auth.GetActiveUsernamesAsync(ct);
        return usernames == null ? NotFound() : Ok(usernames);
    }

    /// <summary>
    /// Mint a fresh token from the (possibly expiring or recently expired) Bearer token in
    /// the Authorization header. Signature + issuer + audience are validated; lifetime is NOT
    /// — that's the whole point of refresh. The new token has a fresh full lifetime.
    ///
    /// Frontends call this proactively a few minutes before the current token expires, or
    /// reactively on a 401 response (then retry the original request with the new token).
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("login")] // share the login rate-limit policy
    public async Task<ActionResult<UserDto>> Refresh(CancellationToken ct)
    {
        var auth = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("ERR_NO_TOKEN", "Token absent.", StatusCodes.Status401Unauthorized);

        var rawToken = auth.Substring(prefix.Length).Trim();

        var tokenKey = _config["AppSettings:TokenKey"]
            ?? throw new InvalidOperationException("AppSettings:TokenKey not configured.");

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            ValidateIssuer = true,
            ValidIssuer = _config["AppSettings:TokenIssuer"] ?? "BakeryPOS.API",
            ValidateAudience = true,
            ValidAudience = _config["AppSettings:TokenAudience"] ?? "BakeryPOS.Client",
            ValidateLifetime = false  // deliberate: refresh accepts expired tokens within signature lifetime
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(rawToken, validationParams, out _);
            var username = principal.FindFirst(JwtRegisteredClaimNames.NameId)?.Value
                ?? principal.FindFirst("nameid")?.Value
                ?? throw new DomainException("ERR_NO_USERNAME", "Token sans utilisateur.", StatusCodes.Status401Unauthorized);

            var user = await _auth.RefreshAsync(username, ct);
            return Ok(user);
        }
        catch (SecurityTokenException)
        {
            throw new DomainException("ERR_INVALID_TOKEN",
                "Token invalide. Veuillez vous reconnecter.",
                StatusCodes.Status401Unauthorized);
        }
    }
}
