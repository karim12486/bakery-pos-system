using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Nizam.Api.Core.Attributes
{
    /// <summary>
    /// [HasPermission(UserPermissions.ManageProducts)] on a controller / action.
    ///
    /// Permission check is the UNION of:
    /// 1. The user's tenant-level <see cref="Core.Entities.User.Permissions"/> bitflag.
    /// 2. If a <c>branch_id</c> claim is present in the JWT, the user's
    ///    <see cref="Core.Entities.UserBranchRole"/> permissions for that specific branch.
    ///
    /// So a user can be granted broad perms tenant-wide via <c>User.Permissions</c>,
    /// AND/OR narrower per-branch perms via UserBranchRole rows. The first user (Admin)
    /// has all bits set tenant-wide and doesn't need branch rows.
    /// </summary>
    public class HasPermissionAttribute : TypeFilterAttribute
    {
        public HasPermissionAttribute(UserPermissions permission) : base(typeof(HasPermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

    public class HasPermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly UserPermissions _requiredPermission;
        private readonly IServiceProvider _serviceProvider;

        public HasPermissionFilter(UserPermissions permission, IServiceProvider serviceProvider)
        {
            _requiredPermission = permission;
            _serviceProvider = serviceProvider;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var usernameClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (usernameClaim == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            var username = usernameClaim.Value;

            // The branch_id claim is optional — if present, we union per-branch grants in.
            var branchIdStr = context.HttpContext.User.FindFirst(CurrentTenant.BranchClaim)?.Value;
            int? branchId = int.TryParse(branchIdStr, out var bId) ? bId : null;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || !user.IsActive)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var effective = user.Permissions;

            if (branchId.HasValue)
            {
                var branchRole = await db.UserBranchRoles
                    .FirstOrDefaultAsync(r => r.UserId == user.Id && r.BranchId == branchId.Value);
                if (branchRole != null)
                {
                    effective |= branchRole.Permissions;
                }
            }

            if (!effective.HasFlag(_requiredPermission))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
