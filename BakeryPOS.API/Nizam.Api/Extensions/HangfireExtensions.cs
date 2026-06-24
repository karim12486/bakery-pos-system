using Nizam.Api.Common.Hangfire;
using Nizam.Api.Services.Jobs;
using Hangfire;
using Hangfire.SqlServer;

namespace Nizam.Api.Extensions;

public static class HangfireExtensions
{
    /// <summary>
    /// Hosting environment name used by the integration test factory. When the app runs in this
    /// environment, every Hangfire entry point in this class becomes a no-op — registration,
    /// recurring-job scheduling, and the dashboard endpoint are all skipped so tests don't
    /// need a SQL Server (Hangfire's <c>RecurringJobManager.AddOrUpdate</c> acquires a SQL
    /// distributed lock, which throws when the configured connection string isn't reachable).
    /// </summary>
    public const string TestingEnvironment = "Testing";

    /// <summary>
    /// Registers Hangfire on top of SQL Server. Hangfire creates its own <c>HangFire</c>
    /// schema in the same DB on first connect — no manual migration needed. The job runner
    /// (server) is registered as a hosted service that pulls jobs from the queue.
    ///
    /// <para>Becomes a no-op in the <see cref="TestingEnvironment"/> environment so
    /// integration tests can start the host without SQL Server.</para>
    /// </summary>
    public static IServiceCollection AddNizamHangfire(
        this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        if (env.IsEnvironment(TestingEnvironment)) return services;

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required for Hangfire SQL storage.");

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,             // event-driven, no polling churn
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true                       // safe with the SQL Server provider
            }));

        // The Hangfire server is the process that actually executes jobs. WebJobs / sidecar
        // deployment can run this server separately with the same connection string and
        // process the same queue.
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
            options.ServerName = $"{Environment.MachineName}:nizam";
        });

        return services;
    }

    /// <summary>
    /// Mounts the Hangfire dashboard at <c>/hangfire</c>, gated behind
    /// <see cref="HangfireDashboardAuthFilter"/> (authenticated + ManageUsers permission).
    ///
    /// <para>No-op in the <see cref="TestingEnvironment"/> environment.</para>
    /// </summary>
    public static WebApplication UseNizamHangfireDashboard(this WebApplication app)
    {
        if (app.Environment.IsEnvironment(TestingEnvironment)) return app;

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthFilter() },
            DisplayStorageConnectionString = false,
            DashboardTitle = "Nizam Jobs"
        });
        return app;
    }

    /// <summary>
    /// Registers the recurring schedule for our jobs. Idempotent — calling on every startup
    /// upserts the schedule by job id. Crons are UTC (Hangfire default for new schedules).
    ///
    /// <para>No-op in the <see cref="TestingEnvironment"/> environment.</para>
    /// </summary>
    public static WebApplication ScheduleNizamRecurringJobs(this WebApplication app)
    {
        if (app.Environment.IsEnvironment(TestingEnvironment)) return app;

        var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

        recurring.AddOrUpdate<DatabaseBackupJob>(
            DatabaseBackupJob.RecurringJobId,
            j => j.RunAsync(CancellationToken.None),
            DatabaseBackupJob.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        recurring.AddOrUpdate<DailyReportsJob>(
            DailyReportsJob.RecurringJobId,
            j => j.RunAsync(CancellationToken.None),
            DailyReportsJob.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        recurring.AddOrUpdate<ReservationReminderJob>(
            ReservationReminderJob.RecurringJobId,
            j => j.RunAsync(CancellationToken.None),
            ReservationReminderJob.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        recurring.AddOrUpdate<TrialDowngradeJob>(
            TrialDowngradeJob.RecurringJobId,
            j => j.RunAsync(CancellationToken.None),
            TrialDowngradeJob.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        return app;
    }
}
