using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using BakeryPOS.API.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AutoMapper;

namespace BakeryPOS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 1. This is the magic attribute!
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IMapper _mapper;

        public AdminController(AppDbContext context, IPasswordService passwordService, IMapper mapper)
        {
            _context = context;
            _passwordService = passwordService;
            _mapper = mapper;
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

        // GET: api/admin/users
        // Gets a list of all users.
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserDetailDto>>> GetUsers()
        {
            var users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            var usersToReturn = _mapper.Map<IEnumerable<UserDetailDto>>(users);
            return Ok(usersToReturn);
        }

        // GET: api/admin/users/5
        // Gets a single user by their ID.
        [HttpGet("users/{id}")]
        public async Task<ActionResult<UserDetailDto>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            var userToReturn = _mapper.Map<UserDetailDto>(user);
            return Ok(userToReturn);
        }

        // PUT: api/admin/users/5
        // Updates a user's details.
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserForUpdateDto userForUpdateDto)
        {
            var userFromDb = await _context.Users.FindAsync(id);
            if (userFromDb == null)
            {
                return NotFound();
            }

            // Use AutoMapper to update the user entity from the DTO
            _mapper.Map(userForUpdateDto, userFromDb);

            await _context.SaveChangesAsync();
            return NoContent(); // Standard response for a successful update
        }

        // DELETE: api/admin/users/5
        // Deactivates a user (soft delete).
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userFromDb = await _context.Users.FindAsync(id);
            if (userFromDb == null)
            {
                return NotFound();
            }

            // Prevent an admin from deactivating themselves
            var currentUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userFromDb.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("You cannot deactivate your own account.");
            }

            // Soft delete: We just mark the user as inactive.
            // This preserves their history in sales records.
            userFromDb.IsActive = false;

            await _context.SaveChangesAsync();
            return NoContent(); // Standard response for a successful delete
        }

        [HttpPost("users/{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto resetPasswordDto)
        {
            var userFromDb = await _context.Users.FindAsync(id);
            if (userFromDb == null)
            {
                return NotFound("User not found.");
            }

            // Hash the new password and update the user
            userFromDb.PasswordHash = _passwordService.HashPassword(resetPasswordDto.NewPassword);

            await _context.SaveChangesAsync();

            return Ok("Password updated successfully.");
        }
    }
}