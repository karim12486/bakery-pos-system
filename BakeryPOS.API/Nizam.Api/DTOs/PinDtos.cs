using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs;

/// <summary>Quick login on a shared POS device. The device knows its tenant (configured once);
/// staff just punch a PIN.</summary>
public class PinLoginDto
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string TenantSlug { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{4,6}$", ErrorMessage = "PIN must be 4 to 6 digits.")]
    public string Pin { get; set; } = string.Empty;
}

/// <summary>Set (or change) a user's PIN. Admin action.</summary>
public class SetPinDto
{
    [Required, RegularExpression(@"^\d{4,6}$", ErrorMessage = "PIN must be 4 to 6 digits.")]
    public string Pin { get; set; } = string.Empty;
}
