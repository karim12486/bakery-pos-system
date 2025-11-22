using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.Data.Seed;
using BakeryPOS.API.Mappers;
using BakeryPOS.API.Services;
using BakeryPOS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Your using statement for Swagger
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using BakeryPOS.API.Hubs;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
 options.AddPolicy(name: MyAllowSpecificOrigins,
 policy =>
 {
 // Allow the dev frontend origins (include scheme + port).
 policy.WithOrigins("http://127.0.0.1:5500", "http://localhost:5500")
 .AllowAnyHeader()
 .AllowAnyMethod()
 .AllowCredentials(); // Required for SignalR to work with credentials / negotiation
 });
});

// Add services to the container.

// Your DbContext Registration
builder.Services.AddDbContext<AppDbContext>(options =>
{
 options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 options.TokenValidationParameters = new TokenValidationParameters
 {
 ValidateIssuerSigningKey = true,
 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["AppSettings:TokenKey"])),
 ValidateIssuer = false, // For simple projects, we don't need to validate who issued the token
 ValidateAudience = false // Or who the token is for
 };

 // Allow the JWT to be passed via query string for SignalR WebSocket negotiate requests
 options.Events = new JwtBearerEvents
 {
 OnMessageReceived = context =>
 {
 var accessToken = context.Request.Query["access_token"].FirstOrDefault();

 // If the request is for our SignalR hub path, read the token from the query string
 var path = context.HttpContext.Request.Path;
 if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs/removal") || path.StartsWithSegments("/hubs")))
 {
 context.Token = accessToken;
 }

 return Task.CompletedTask;
 }
 };
 });

builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, TelegramNotificationService>();

builder.Services.AddControllers()
 .AddJsonOptions(options =>
 {
 options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
 });
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration
builder.Services.AddSwaggerGen(options =>
{
 options.SwaggerDoc("v1", new OpenApiInfo { Title = "BakeryPOS API", Version = "v1" });

 //1. Define the security scheme (how to use the JWT)
 options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
 {
 In = ParameterLocation.Header,
 Description = "Please enter a valid token",
 Name = "Authorization",
 Type = SecuritySchemeType.Http,
 BearerFormat = "JWT",
 Scheme = "Bearer"
 });

 //2. Add the security requirement (tells Swagger which endpoints need the token)
 options.AddSecurityRequirement(new OpenApiSecurityRequirement
 {
 {
 new OpenApiSecurityScheme
 {
 Reference = new OpenApiReference
 {
 Type = ReferenceType.SecurityScheme,
 Id = "Bearer"
 }
 },
 new string[]{}
 },
 {
 new OpenApiSecurityScheme
 {
 Name = "X-Cashier-Connection-Id",
 In = ParameterLocation.Header,
 Type = SecuritySchemeType.ApiKey, // Use ApiKey for custom headers
 Description = "The SignalR connection ID of the cashier who made the request (for testing)."
 },
 new List<string>()
 }
 });
});

builder.Services.AddAutoMapper(cfg =>
{
 cfg.AddMaps(typeof(AutoMapperProfiles).Assembly);
});


builder.Services.AddHostedService<ScheduledReportService>();

builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();

builder.Services.AddSignalR();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
 var services = scope.ServiceProvider;
 try
 {
 var context = services.GetRequiredService<AppDbContext>();
 var passwordService = services.GetRequiredService<IPasswordService>();
 await DbInitializer.Initialize(context, passwordService);
 }
 catch (Exception ex)
 {
 var logger = services.GetRequiredService<ILogger<Program>>();
 logger.LogError(ex, "An error occurred while seeding the database.");
 }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
 // Your excellent Swagger UI configuration
 app.UseSwaggerUI(c =>
 {
 c.SwaggerEndpoint("/swagger/v1/swagger.json", "BakeryPOS API V1");
 c.RoutePrefix = string.Empty; // Serve Swagger UI at the root
 });
}

app.UseHttpsRedirection();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();

app.UseAuthorization();

var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var imagesDir = Path.Combine(webRoot, "images");
if (!Directory.Exists(imagesDir))
{
    Directory.CreateDirectory(imagesDir);
}
app.UseStaticFiles();

app.MapControllers();

// --- ADD THIS BLOCK TO FIX CURRENCY SYMBOL ---
//var defaultCulture = new CultureInfo("en-US"); // Use "en-US" for $ (Dollars)
 var defaultCulture = new CultureInfo("fr-MA"); // Use "fr-MA" for MAD (Moroccan Dirham)
//var defaultCulture = new CultureInfo("ar-EG"); // Use "ar-EG" for EGP (Egyptian Pound)

defaultCulture.NumberFormat.CurrencySymbol = "$"; // Optional: Force a specific symbol if needed

CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

app.MapHub<RemovalHub>("/hubs/removal");

app.Run();

public partial class Program { } // For integration testing purposes