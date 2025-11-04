using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 1. This is the magic attribute!
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;

        public AdminController(AppDbContext context, IPasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser(UserForCreationDto userForCreationDto)
        {
            // 2. Check if username already exists to prevent duplicates
            var usernameLower = userForCreationDto.Username.ToLower();
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            {
                return BadRequest("Username is already taken.");
            }

            // 3. Create the new user entity
            var newUser = new User
            {
                Username = userForCreationDto.Username,
                FullName = userForCreationDto.FullName,
                PasswordHash = _passwordService.HashPassword(userForCreationDto.Password),
                Permissions = userForCreationDto.Permissions,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // 4. Add the user to the database and save
            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            // 5. Return a success response
            return StatusCode(201, "User created successfully.");
        }
    }
}