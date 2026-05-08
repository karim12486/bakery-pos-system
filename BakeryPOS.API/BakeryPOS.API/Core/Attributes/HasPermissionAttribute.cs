using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BakeryPOS.API.Core.Attributes
{
    // [HasPermission(UserPermissions.ManageProducts)]
    public class HasPermissionAttribute : TypeFilterAttribute
    {
        public HasPermissionAttribute(UserPermissions permission) : base(typeof(HasPermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

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
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            var username = userIdClaim.Value;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = dbContext.Users.FirstOrDefault(u => u.Username == username);

                if (user == null || !user.IsActive)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                if (!user.Permissions.HasFlag(_requiredPermission))
                {
                    context.Result = new ForbidResult();
                }
            }
        }
    }
}
