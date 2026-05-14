using BakeryPOS.API.Core.Entities;

namespace BakeryPOS.API.Core.Interfaces
{
    public interface ITokenService
    {
        /// <summary>Creates a JWT for the user with optional branch scoping (Open POS Shift flow).</summary>
        string CreateToken(User user, int? branchId = null);
    }
}
