using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Services;

namespace BakeryPOS.API.Extensions;

public static class ApplicationServicesExtensions
{
    /// <summary>
    /// Registers the application's domain services (auth, reports, PDFs, notifications)
    /// and long-running background workers (DB backup, scheduled reports).
    /// </summary>
    public static IServiceCollection AddBakeryPosApplicationServices(this IServiceCollection services)
    {
        // Per-request domain services
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IReportGenerationService, ReportGenerationService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<INotificationService, TelegramNotificationService>();

        // Outbound HTTP (used by the Telegram notifier; pooled by the framework)
        services.AddHttpClient();

        // Background workers
        services.AddHostedService<DatabaseBackupService>();
        services.AddHostedService<ScheduledReportService>();

        return services;
    }
}
