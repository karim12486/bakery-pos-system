using Nizam.Api.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs
{
    /// <summary>
    /// Validation is via DataAnnotations + [ApiController] auto-validation. Rich cross-field
    /// rules go through FluentValidation only when the service explicitly injects IValidator&lt;T&gt;
    /// (see SaleForCreateDtoValidator). Don't duplicate rules across both.
    /// </summary>
    public class UserForCreationDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Le nom d'utilisateur doit comporter entre 3 et 50 caractères.")]
        [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Le nom d'utilisateur ne peut contenir que des lettres, chiffres, '.', '_' ou '-'.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Le mot de passe doit comporter au moins 8 caractères.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$",
            ErrorMessage = "Le mot de passe doit contenir au moins une lettre et un chiffre.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserPermissions Permissions { get; set; }

        public string? ImageUrl { get; set; }

        public string? Role { get; set; }
    }
}
