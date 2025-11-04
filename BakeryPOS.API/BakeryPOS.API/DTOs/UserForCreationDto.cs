using BakeryPOS.API.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class UserForCreationDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserPermissions Permissions { get; set; }
    }
}