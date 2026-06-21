using Nizam.Api.Core.Entities;

namespace Nizam.Api.Core.Interfaces
{
    public interface ITokenService
    {
        /// <summary>Creates a JWT for the user with optional branch scoping (Open POS Shift flow).</summary>
        string CreateToken(User user, int? branchId = null);

        /// <summary>Creates a JWT for a platform operator — carries the <c>super_admin</c> claim
        /// and NO tenant_id (super-admins are cross-tenant).</summary>
        string CreateSuperAdminToken(SuperAdmin admin);
    }

    /// <summary>Well-known JWT claim names used across auth.</summary>
    public static class NizamClaims
    {
        public const string SuperAdmin = "super_admin";
    }
}
