using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BakeryPOS.API.Services
{
    public class TokenService : ITokenService
    {
        private readonly SymmetricSecurityKey _key;

        public TokenService(IConfiguration config)
        {
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["AppSettings:TokenKey"]));
        }

        public string CreateToken(User user)
        {
            // 1. Create the claims (the information you want in the token)
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, user.Username)
                // You can add more claims here, like user.Id, roles, etc.
            };

            // 2. Create the signing credentials using the secret key and a security algorithm
            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

            // 3. Describe the token: who it's for, what claims it has, when it expires, and what credentials were used to sign it
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddHours(12), // Token is valid for 12 hours
                SigningCredentials = creds
            };

            // 4. Create the token handler that will actually create the token
            var tokenHandler = new JwtSecurityTokenHandler();

            // 5. Create the token
            var token = tokenHandler.CreateToken(tokenDescriptor);

            // 6. Write the token into the string format that we can send to the client
            return tokenHandler.WriteToken(token);
        }
    }
}