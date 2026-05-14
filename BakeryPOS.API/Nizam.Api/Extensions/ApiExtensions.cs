using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.OpenApi.Models;

namespace Nizam.Api.Extensions;

public static class ApiExtensions
{
    public const string CorsPolicyName = "_bakeryPosCorsPolicy";

    /// <summary>
    /// Registers the CORS policy. Reads <c>Cors:AllowedOrigins</c> as an explicit allow-list with
    /// credentials enabled. If no origins are configured, falls back to a permissive (LAN-friendly)
    /// policy with credentials disabled.
    /// </summary>
    public static IServiceCollection AddNizamCors(this IServiceCollection services, IConfiguration config)
    {
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    // LAN-friendly default: any origin, but NO credentials.
                    // Bearer tokens in headers continue to work; cookie-based auth is blocked.
                    policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
            });
        });
        return services;
    }

    /// <summary>
    /// Adds MVC controllers (with the JsonStringEnumConverter so enums serialize as strings),
    /// and Swagger with bearer + the cashier-connection-id header documented as security schemes.
    /// </summary>
    public static IServiceCollection AddNizamApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddEndpointsApiExplorer();

        // FluentValidation — automatically discovers all AbstractValidator<T> in this assembly.
        // ValidationException thrown by manual validation calls (or by FluentValidation auto-validation
        // if enabled later) is caught by ProblemDetailsMiddleware and returned as RFC 7807.
        services.AddValidatorsFromAssemblyContaining<Program>();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Nizam API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter a valid token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    new string[] { }
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Name = "X-Cashier-Connection-Id",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Description = "The SignalR connection ID of the cashier who made the request."
                    },
                    new List<string>()
                }
            });
        });

        return services;
    }
}
