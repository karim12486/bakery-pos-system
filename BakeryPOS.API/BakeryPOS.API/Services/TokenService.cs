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
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _lifetimeHours;

        public TokenService(IConfiguration config)
        {
            var tokenKey = config["AppSettings:TokenKey"];
            if (string.IsNullOrWhiteSpace(tokenKey))
            {
                throw new InvalidOperationException("AppSettings:TokenKey is not configured.");
            }
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));
            _issuer = config["AppSettings:TokenIssuer"] ?? "BakeryPOS.API";
            _audience = config["AppSettings:TokenAudience"] ?? "BakeryPOS.Client";
            _lifetimeHours = config.GetValue<int?>("AppSettings:TokenLifetimeHours") ?? 12;
        }

        public string CreateToken(User user)
        {
            // NameId stays as the username for backwards-compat with controllers
            // that read ClaimTypes.NameIdentifier. uid carries the numeric user id.
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, user.Username),
                new Claim("uid", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddHours(_lifetimeHours),
                SigningCredentials = creds,
                Issuer = _issuer,
                Audience = _audience
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
