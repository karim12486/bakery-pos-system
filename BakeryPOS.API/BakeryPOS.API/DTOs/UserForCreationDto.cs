using BakeryPOS.API.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class UserForCreationDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Le nom d'utilisateur doit comporter entre 3 et 50 caractères.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Le mot de passe doit comporter au moins 6 caractères.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserPermissions Permissions { get; set; }

        public string? ImageUrl { get; set; } // Added for the new image feature
    }
}