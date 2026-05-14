using Nizam.Api.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs
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