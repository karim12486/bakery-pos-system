using BakeryPOS.API.Common.Tenancy;
using BakeryPOS.API.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BakeryPOS.API.Hubs
{
    /// <summary>
    /// Hub for the cashier-requests-removal / admin-approves flow.
    /// Group names are tenant-scoped so Tenant A's removal requests never reach Tenant B's admins.
    /// </summary>
    [Authorize]
    public class RemovalHub : Hub
    {
        /// <summary>
        /// Returns the tenant-scoped admin-group name. Server-side helper for the controller too.
        /// </summary>
        public static string AdminGroup(int tenantId) => $"Admins:{tenantId}";

        /// <summary>
        /// Called by an admin client to subscribe to removal-request notifications for THEIR tenant.
        /// Verifies the caller actually has ApproveRemovals permission — otherwise a regular cashier
        /// could join the admin group and see every removal request.
        /// </summary>
        public async Task JoinAdminsGroup()
        {
            var tenantIdClaim = Context.User?.FindFirst(CurrentTenant.TenantClaim)?.Value;
            if (!int.TryParse(tenantIdClaim, out var tenantId))
            {
                throw new HubException("Tenant context missing.");
            }

            var permsClaim = Context.User?.FindFirst("permissions")?.Value;
            // Permission check: must have ApproveRemovals. The legacy bitflag enum stores int.
            if (!int.TryParse(permsClaim, out var permsInt) ||
                !((UserPermissions)permsInt).HasFlag(UserPermissions.ApproveRemovals))
            {
                throw new HubException("Forbidden.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup(tenantId));
        }
    }
}
