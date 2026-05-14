using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Nizam.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Validates that <c>AppSettings:TokenKey</c> is present and at least 64 bytes (HMAC-SHA512 minimum),
    /// then registers JWT bearer auth with issuer/audience/lifetime validation and the SignalR
    /// query-string token reader scoped to <c>/hubs/*</c> paths only.
    /// </summary>
    public static IServiceCollection AddNizamAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var tokenKey = config["AppSettings:TokenKey"];
        if (string.IsNullOrWhiteSpace(tokenKey) || Encoding.UTF8.GetByteCount(tokenKey) < 64)
        {
            throw new InvalidOperationException(
                "AppSettings:TokenKey is missing or shorter than 64 bytes. " +
                "Set it via environment variable (AppSettings__TokenKey) or user-secrets. " +
                "Use a cryptographically random value of at least 64 bytes for HMAC-SHA512.");
        }

        var tokenIssuer = config["AppSettings:TokenIssuer"] ?? "Nizam.Api";
        var tokenAudience = config["AppSettings:TokenAudience"] ?? "Nizam.Client";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
                    ValidateIssuer = true,
                    ValidIssuer = tokenIssuer,
                    ValidateAudience = true,
                    ValidAudience = tokenAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                // SignalR token in query string — only honoured on hub paths to limit exposure window.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var path = ctx.HttpContext.Request.Path;
                        if (path.StartsWithSegments("/hubs"))
                        {
                            var accessToken = ctx.Request.Query["access_token"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                ctx.Token = accessToken;
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Per-IP fixed-window rate limit applied to the <c>"login"</c> policy
    /// (used by <c>[EnableRateLimiting("login")]</c> on <c>AuthController.Login</c>).
    /// Defaults to 10 attempts per 60s, overridable via <c>Auth:LoginRateLimit</c>.
    /// </summary>
    public static IServiceCollection AddNizamRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var rateCfg = config.GetSection("Auth:LoginRateLimit");
        var permitLimit = rateCfg.GetValue<int?>("PermitLimit") ?? 10;
        var windowSeconds = rateCfg.GetValue<int?>("WindowSeconds") ?? 60;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
