using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs;

/// <summary>
/// Bootstraps a brand-new tenant + their first branch + first admin user, all in one
/// atomic transaction. The returned UserDto carries a JWT that already has the new
/// tenant_id baked in, so the frontend can immediately call any tenant-scoped endpoint
/// (e.g. Branch Select) without re-logging in.
/// </summary>
public class TenantSignupDto
{
    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string BusinessName { get; set; } = string.Empty;

    /// <summary>URL-safe slug — used in subdomain routing later. <c>my-bakery</c>, <c>al-rashid</c>.</summary>
    [Required]
    [StringLength(60, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug : lettres minuscules, chiffres et tiret uniquement.")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>BCP-47 locale, e.g. <c>ar-EG</c>, <c>en-US</c>. Defaults to <c>ar-EG</c> if empty.</summary>
    [StringLength(20)]
    public string? Locale { get; set; }

    /// <summary>ISO-4217 currency, e.g. <c>EGP</c>. Defaults to <c>EGP</c> if empty.</summary>
    [StringLength(3)]
    public string? Currency { get; set; }

    // First branch — every tenant has at least one branch to operate from.

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>IANA tz name, e.g. <c>Africa/Cairo</c>. Defaults to <c>Africa/Cairo</c>.</summary>
    [StringLength(60)]
    public string? BranchTimezone { get; set; }

    // First admin — the person signing up becomes the tenant owner.

    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Le nom d'utilisateur ne peut contenir que des lettres, chiffres, '.', '_' ou '-'.")]
    public string AdminUsername { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$",
        ErrorMessage = "Le mot de passe doit contenir au moins une lettre et un chiffre.")]
    public string AdminPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string AdminFullName { get; set; } = string.Empty;
}

/// <summary>Outcome of a successful signup — the new tenant + the admin's first JWT.</summary>
public class TenantSignupResultDto
{
    public int TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public UserDto Admin { get; set; } = new();
}
