using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly ITokenService _tokenService;

        // 1. Inject all the required services
        public AuthController(AppDbContext context, IPasswordService passwordService, ITokenService tokenService)
        {
            _context = context;
            _passwordService = passwordService;
            _tokenService = tokenService;
        }

        [HttpGet("hash/{password}")]
        public ActionResult<string> GetHash(string password)
        {
            var hash = _passwordService.HashPassword(password);
            return Ok(hash);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(UserForLoginDto userForLoginDto)
        {
            // 2. Find the user by username. Case-insensitive search is good practice.
            var user = await _context.Users
                .SingleOrDefaultAsync(u => u.Username.ToLower() == userForLoginDto.Username.ToLower());

            // 3. Check if the user exists
            if (user == null)
            {
                return Unauthorized("Nom d'utilisateur ou mot de passe incorrect.");
            }

            // 4. Verify the password using our service
            var isPasswordValid = _passwordService.VerifyPassword(userForLoginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized("Nom d'utilisateur ou mot de passe incorrect.");
            }

            // Determine Role based on permissions (Simple logic)
            string role = user.Permissions.HasFlag(Core.Enums.UserPermissions.Admin) ? "Admin" : "Cashier";

            // Convert Enum flags to a list of strings
            var permissionsList = Enum.GetValues(typeof(Core.Enums.UserPermissions))
                .Cast<Core.Enums.UserPermissions>()
                .Where(p => user.Permissions.HasFlag(p) && p != Core.Enums.UserPermissions.None && p != Core.Enums.UserPermissions.Admin)
                .Select(p => p.ToString())
                .ToList();

            // 5. If everything is correct, create and return the UserDto with a token
            var userDto = new UserDto
            {
                Username = user.Username,
                FullName = user.FullName,
                Token = _tokenService.CreateToken(user),
                Role = role,
                Permissions = permissionsList,
                ImageUrl = user.ImageUrl
            };

            return Ok(userDto);
        }

        // GET: api/auth/users
        // Public endpoint to get just the usernames for the login screen
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<string>>> GetUsernames()
        {
            var usernames = await _context.Users
                .Where(u => u.IsActive)       // Only active users
                .OrderBy(u => u.Username)     // Sort alphabetically
                .Select(u => u.Username)      // Select ONLY the username string
                .ToListAsync();

            return Ok(usernames);
        }
    }
}