using BakeryPOS.API.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class UserForUpdateDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        [Required]
        public int Permissions { get; set; }
        public bool IsActive { get; set; }
        public string? ImageUrl { get; set; }
        public string? Role { get; set; }
    }
}