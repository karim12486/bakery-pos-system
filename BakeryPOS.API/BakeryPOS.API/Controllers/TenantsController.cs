using BakeryPOS.API.DTOs;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BakeryPOS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TenantsController : ControllerBase
{
    private readonly ITenantSignupService _signup;

    public TenantsController(ITenantSignupService signup)
    {
        _signup = signup;
    }

    /// <summary>
    /// Create a new tenant in one shot: business + first branch + first admin user.
    /// Anonymous endpoint — anyone can create a tenant. Rate-limited under the "login" policy
    /// so a hostile client can't spin up tenants forever.
    ///
    /// Returns a JWT for the new admin so the frontend can transition straight into the app
    /// (Branch Select → Open Shift → Cashier).
    /// </summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<TenantSignupResultDto>> Signup(TenantSignupDto dto, CancellationToken ct)
    {
        var result = await _signup.SignupAsync(dto, ct);
        return CreatedAtAction(nameof(Signup), new { id = result.TenantId }, result);
    }
}
