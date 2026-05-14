using Nizam.Api.Core.Enums;
using Hangfire.Dashboard;

namespace Nizam.Api.Common.Hangfire;

/// <summary>
/// Gates <c>/hangfire</c> behind authentication AND the <c>ManageUsers</c> permission.
///
/// Hangfire's default dashboard is permissive — bare unauthenticated access from localhost only.
/// For SaaS deployment we want to make sure tenant admins (and eventually a super-admin role)
/// are the only ones who see the job queue.
///
/// TODO[super-admin]: when the platform super-admin role lands, switch this to require it.
/// Today's check leaks the dashboard to any tenant admin — they can see/cancel their tenant's
/// jobs but also see every other tenant's job NAMES. Not data, but still cross-tenant info.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http?.User?.Identity?.IsAuthenticated != true) return false;

        var permsClaim = http.User.FindFirst("permissions")?.Value;
        if (!int.TryParse(permsClaim, out var perms)) return false;

        return ((UserPermissions)perms).HasFlag(UserPermissions.ManageUsers);
    }
}
