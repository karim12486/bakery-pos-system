using Nizam.Api.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Nizam.Api.Core.Attributes;

/// <summary>
/// Restricts an endpoint to authenticated platform operators — requires the <c>super_admin</c>
/// JWT claim. Tenant users (who never carry that claim) get 403. Pair with <c>[Authorize]</c>
/// so the token is validated first.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SuperAdminOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var isSuperAdmin = context.HttpContext.User?.FindFirst(NizamClaims.SuperAdmin)?.Value == "true";
        if (!isSuperAdmin)
            context.Result = new ForbidResult();
    }
}
