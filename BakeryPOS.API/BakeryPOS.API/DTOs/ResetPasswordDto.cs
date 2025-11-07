using System.ComponentModel.DataAnnotations;

public class ResetPasswordDto
{
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; }
}