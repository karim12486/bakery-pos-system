using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.Data.Seed;
using BakeryPOS.API.Mappers;
using BakeryPOS.API.Services;
using BakeryPOS.API.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ---- Required-secret validation: fail fast with a clear message instead of NRE / weak defaults
var tokenKey = builder.Configuration["AppSettings:TokenKey"];
if (string.IsNullOrWhiteSpace(tokenKey) || Encoding.UTF8.GetByteCount(tokenKey) < 64)
{
    throw new InvalidOperationException(
        "AppSettings:TokenKey is missing or shorter than 64 bytes. " +
        "Set it via environment variable (AppSettings__TokenKey) or user-secrets. " +
        "Use a cryptographically random value of at least 64 bytes for HMAC-SHA512.");
}
var tokenIssuer = builder.Configuration["AppSettings:TokenIssuer"] ?? "BakeryPOS.API";
var tokenAudience = builder.Configuration["AppSettings:TokenAudience"] ?? "BakeryPOS.Client";

// 1. CORS — explicit allow-list. Falls back to localhost only if config is empty.
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // Safe LAN-friendly default: any origin, but NO credentials and NO wildcard subdomain trust.
            // Browsers will still block cookie-based auth; bearer tokens in headers continue to work.
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

// 2. Database Connection
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 3. Services
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, TelegramNotificationService>();
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();
builder.Services.AddHostedService<DatabaseBackupService>();
builder.Services.AddHostedService<ScheduledReportService>();

// 4. AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddMaps(typeof(AutoMapperProfiles).Assembly);
});

// 5. Authentication & SignalR
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

        // SignalR Token Logic — only honour query token on hub paths to limit exposure window
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs"))
                {
                    var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR();

// 5b. Rate limiting — protect login from brute-force / credential-stuffing
var rateCfg = builder.Configuration.GetSection("Auth:LoginRateLimit");
var permitLimit = rateCfg.GetValue<int?>("PermitLimit") ?? 10;
var windowSeconds = rateCfg.GetValue<int?>("WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
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

// 6. Controllers & Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BakeryPOS API", Version = "v1" });
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
            new string[]{}
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

var app = builder.Build();

// 7. Seed Admin User on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var passwordService = services.GetRequiredService<IPasswordService>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        await DbInitializer.Initialize(context, passwordService, logger, app.Environment.ContentRootPath);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 8. Middleware Pipeline

// Enable Swagger in Production too (so you can seed data on the client's PC)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BakeryPOS API V1");
    c.RoutePrefix = string.Empty;
});

// app.UseHttpsRedirection(); // Intentionally off for local LAN HTTP. See README.
app.UseCors(MyAllowSpecificOrigins);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 9. Static Files (Robust Fix for .exe deployment)
var contentRoot = app.Environment.ContentRootPath;
var webRootPath = Path.Combine(contentRoot, "wwwroot");

Console.WriteLine("--------------------------------------------------");
Console.WriteLine($"[DEBUG] Content Root: {contentRoot}");
Console.WriteLine($"[DEBUG] Looking for wwwroot at: {webRootPath}");
Console.WriteLine("--------------------------------------------------");

if (!Directory.Exists(webRootPath))
{
    Console.WriteLine($"[DEBUG] Creating missing directory: {webRootPath}");
    Directory.CreateDirectory(webRootPath);
}

var imagesPath = Path.Combine(webRootPath, "images");
if (!Directory.Exists(imagesPath))
{
    Console.WriteLine($"[DEBUG] Creating missing directory: {imagesPath}");
    Directory.CreateDirectory(imagesPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath),
    RequestPath = ""
});

app.MapControllers();
app.MapHub<RemovalHub>("/hubs/removal");

// 10. Localization (French)
var defaultCulture = new CultureInfo("fr-MA");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

app.Run();

public partial class Program { }
