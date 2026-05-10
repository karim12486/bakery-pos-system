using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BakeryPOS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IPasswordService _passwordService;

    public AuthController(IAuthService auth, IPasswordService passwordService)
    {
        _auth = auth;
        _passwordService = passwordService;
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
}
