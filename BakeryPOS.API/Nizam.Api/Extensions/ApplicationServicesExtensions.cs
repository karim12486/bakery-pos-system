using Nizam.Api.Common.Idempotency;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Services;
using Nizam.Api.Services.Jobs;
using Nizam.Api.Services.Plans;

namespace Nizam.Api.Extensions;

public static class ApplicationServicesExtensions
{
    /// <summary>
    /// Registers the application's domain services (auth, reports, PDFs, notifications)
    /// and long-running background workers (DB backup, scheduled reports).
    /// </summary>
    public static IServiceCollection AddNizamApplicationServices(this IServiceCollection services)
    {
        // Per-request domain services
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IReportGenerationService, ReportGenerationService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<INotificationService, TelegramNotificationService>();

        // Per-feature application services. Controllers are thin shells that bind →
        // delegate to these. Each service centralises validation, transaction wrapping,
        // and DomainException throws for its feature.
        //
        // NOT extracted (intentional): Categories, Images, Inventory, Reports, Removal,
        // Dashboard — all thin CRUD/read paths where direct EF in the controller is
        // simpler and EF Core global query filters will handle multi-tenancy at the
        // DbContext level when that lands.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ITenantSignupService, TenantSignupService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IModifierGroupService, ModifierGroupService>();
        services.AddScoped<IModifierApplicationService, ModifierApplicationService>();

        // Subscription plan service — caches the current tenant's plan/features/limits
        // for the request scope and shares a process-wide IMemoryCache across requests
        // (5-min sliding TTL, invalidated on plan change by the super-admin portal).
        services.AddMemoryCache();
        services.AddScoped<IPlanService, PlanService>();

        // Outbound HTTP (used by the Telegram notifier; pooled by the framework)
        services.AddHttpClient();

        // Hangfire job CLASSES (the recurring schedule is set up in HangfireExtensions).
        // Hangfire activates these via DI per execution, so they're registered transient — each
        // job invocation gets a fresh instance with fresh AppDbContext.
        services.AddTransient<DatabaseBackupJob>();
        services.AddTransient<DailyReportsJob>();

        return services;
    }
}
