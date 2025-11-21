using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BakeryPOS.API.Core.Attributes
{
    // This is the attribute you use on the Controller: [HasPermission(UserPermissions.ManageProducts)]
    public class HasPermissionAttribute : TypeFilterAttribute
    {
        public HasPermissionAttribute(UserPermissions permission) : base(typeof(HasPermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

    // This is the logic that runs every time the attribute is used
    public class HasPermissionFilter : IAuthorizationFilter
    {
        private readonly UserPermissions _requiredPermission;
        private readonly IServiceProvider _serviceProvider;

        public HasPermissionFilter(UserPermissions permission, IServiceProvider serviceProvider)
        {
            _requiredPermission = permission;
            _serviceProvider = serviceProvider;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // 1. Check if user is authenticated (logged in)
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // 2. Get the User ID from the claims
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier); // In TokenService we stored Username here
            if (userIdClaim == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            var username = userIdClaim.Value;

            // 3. We need to get the DbContext to check the latest permissions from the DB
            // We use a scope because Filters are singletons but DbContext is scoped
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Find the user in the DB
                var user = dbContext.Users.FirstOrDefault(u => u.Username == username);

                if (user == null)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                // 4. THE CHECK: Does the user have the required permission?
                // We use the bitwise 'HasFlag' method.
                if (!user.Permissions.HasFlag(_requiredPermission))
                {
                    // If not, return 403 Forbidden
                    context.Result = new ForbidResult();
                }
            }
        }
    }
}