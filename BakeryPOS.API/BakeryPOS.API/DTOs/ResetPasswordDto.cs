using System.ComponentModel.DataAnnotations;

public class ResetPasswordDto
{
    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Le mot de passe doit comporter au moins 8 caractères.")]
    [RegularExpression(
        @"^(?=.*[A-Za-z])(?=.*\d).{8,100}$",
        ErrorMessage = "Le mot de passe doit contenir au moins une lettre et un chiffre.")]
    public string NewPassword { get; set; } = string.Empty;
}
