using BakeryPOS.API.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class UserForUpdateDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserPermissions Permissions { get; set; }

        public bool IsActive { get; set; }
    }
}