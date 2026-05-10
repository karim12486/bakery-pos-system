using BakeryPOS.API.Common.Tenancy;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.Data.Seed;
using BakeryPOS.API.Mappers;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Extensions;

public static class PersistenceExtensions
{
    /// <summary>
    /// Registers the EF Core <see cref="AppDbContext"/> against the configured SQL Server connection,
    /// plus AutoMapper profiles from this assembly.
    /// </summary>
    public static IServiceCollection AddBakeryPosPersistence(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. " +
                "Set it via appsettings, environment variable, or user-secrets.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(typeof(AutoMapperProfiles).Assembly);
        });

        // Tenancy. ICurrentTenant is request-scoped and reads tenant_id from the JWT claim
        // (CurrentTenant implementation); EF global query filters in AppDbContext depend on it.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, CurrentTenant>();

        return services;
    }

    /// <summary>
    /// Applies pending EF migrations and seeds the initial admin user. Safe to call on every startup —
    /// the seeder is idempotent.
    /// </summary>
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();
        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            var passwordService = services.GetRequiredService<IPasswordService>();
            await DbInitializer.Initialize(context, passwordService, logger, app.Environment.ContentRootPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}
