using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // BCrypt hash of a random throwaway value, computed once. Used to spend
        // verification time on the "user not found" branch so login latency
        // doesn't reveal whether a username exists.
        private static readonly string DummyHash =
            BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IPasswordService passwordService, ITokenService tokenService, IConfiguration config)
        {
            _context = context;
            _passwordService = passwordService;
            _tokenService = tokenService;
            _config = config;
        }

        [HttpGet("hash/{password}")]
        public ActionResult<string> GetHash(string password)
        {
            var hash = _passwordService.HashPassword(password);
            return Ok(hash);
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<UserDto>> Login(UserForLoginDto userForLoginDto)
        {
            const string genericError = "Nom d'utilisateur ou mot de passe incorrect.";

            var user = await _context.Users
                .SingleOrDefaultAsync(u => u.Username.ToLower() == userForLoginDto.Username.ToLower());

            // Always run BCrypt verify — same wall-clock cost whether the user exists or not.
            bool isPasswordValid;
            if (user != null)
            {
                isPasswordValid = _passwordService.VerifyPassword(userForLoginDto.Password, user.PasswordHash);
            }
            else
            {
                _passwordService.VerifyPassword(userForLoginDto.Password, DummyHash);
                isPasswordValid = false;
            }

            if (user == null || !user.IsActive || !isPasswordValid)
            {
                return Unauthorized(genericError);
            }

            string role = !string.IsNullOrEmpty(user.Role)
                ? user.Role
                : (user.Permissions.HasFlag(Core.Enums.UserPermissions.Admin) ? "Admin" : "Cashier");

            var userDto = new UserDto
            {
                Username = user.Username,
                FullName = user.FullName,
                Token = _tokenService.CreateToken(user),
                Role = role,
                Permissions = (int)user.Permissions,
                ImageUrl = user.ImageUrl
            };

            return Ok(userDto);
        }

        // GET: api/auth/users
        // Returns active usernames for the kiosk-style login picker.
        // Disable in production by setting Auth:AllowUsernameListing=false.
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<string>>> GetUsernames()
        {
            var allowed = _config.GetValue<bool?>("Auth:AllowUsernameListing") ?? true;
            if (!allowed)
            {
                return NotFound();
            }

            var usernames = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username)
                .Select(u => u.Username)
                .ToListAsync();

            return Ok(usernames);
        }
    }
}
