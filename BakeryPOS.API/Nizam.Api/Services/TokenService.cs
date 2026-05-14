using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Nizam.Api.Services
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
            _issuer = config["AppSettings:TokenIssuer"] ?? "Nizam.Api";
            _audience = config["AppSettings:TokenAudience"] ?? "Nizam.Client";
            _lifetimeHours = config.GetValue<int?>("AppSettings:TokenLifetimeHours") ?? 12;
        }

        public string CreateToken(User user, int? branchId = null)
        {
            // NameId stays as the username for backwards-compat with controllers
            // that read ClaimTypes.NameIdentifier. uid carries the numeric user id.
            // tenant_id is the multi-tenant scope — CurrentTenant reads it on every request.
            // branch_id, when present, narrows the scope further (set after Branch Select).
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, user.Username),
                new Claim("uid", user.Id.ToString()),
                new Claim("tenant_id", user.TenantId.ToString()),
                // Permissions bitflag — consumed by RemovalHub for fine-grained SignalR group
                // join authorization. HTTP endpoints still go through the DB-backed HasPermission
                // filter for revocation-on-the-spot semantics.
                new Claim("permissions", ((int)user.Permissions).ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (branchId.HasValue)
            {
                claims.Add(new Claim("branch_id", branchId.Value.ToString()));
            }

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
