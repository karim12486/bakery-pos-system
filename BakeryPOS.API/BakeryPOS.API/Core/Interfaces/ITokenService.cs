using BakeryPOS.API.Core.Entities;

namespace BakeryPOS.API.Core.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}