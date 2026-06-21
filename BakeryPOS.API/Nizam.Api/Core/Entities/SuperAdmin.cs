namespace Nizam.Api.Core.Entities;

/// <summary>
/// A NIZAM-team operator — NOT a tenant user. Super-admins manage the platform: list tenants,
/// suspend/reactivate, change plans, grant add-ons. They have NO tenant scope (their JWT carries
/// a <c>super_admin</c> claim and no <c>tenant_id</c>), and the super-admin endpoints reach
/// across tenants via <c>IgnoreQueryFilters</c>.
///
/// <para>Deliberately a separate table from <see cref="User"/> so platform staff and tenant
/// staff can never be confused, and so a compromised tenant admin can't escalate to platform
/// control.</para>
/// </summary>
public class SuperAdmin
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
