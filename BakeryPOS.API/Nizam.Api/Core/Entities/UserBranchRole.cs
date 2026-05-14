using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// Many-to-many: a User can be assigned different permission sets at different Branches.
/// Examples:
///   - A cashier assigned only to "Maadi" branch with <see cref="UserPermissions.ProcessSales"/>
///   - A manager covering "Maadi" + "Zamalek" with broader perms at each
///   - The tenant owner with all perms at all branches (typically also represented by
///     <see cref="User.Permissions"/> = <c>Admin</c>, see <c>HasPermissionFilter</c>)
///
/// The legacy <see cref="User.Permissions"/> bitflag still exists and is treated as a
/// TENANT-LEVEL grant — applies to all branches in that user's tenant. <c>UserBranchRole</c>
/// rows narrow that to specific branches and/or grant additional permissions at specific
/// branches. The authz filter is the union.
/// </summary>
public class UserBranchRole
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Permissions granted at this specific branch.</summary>
    public UserPermissions Permissions { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
