using BakeryPOS.API.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services.Jobs;

/// <summary>
/// Hangfire-scheduled daily backup. Runs <c>BACKUP DATABASE</c> as raw SQL — outside EF,
/// since this is a DDL/management command, not a model query. Result lands in a folder
/// named <c>Backups/</c> next to the app executable.
///
/// Replaces the legacy <c>DatabaseBackupService : BackgroundService</c> which lived inside
/// the host process and lost its schedule on every restart. Hangfire persists schedule state
/// in the DB, so a missed run after a restart is recovered automatically.
///
/// <para>Single-DB caveat:</para>
/// This backs up the entire shared multi-tenant DB to a single .bak file. Fine for the
/// on-prem .exe deployment and single-tenant SaaS scenarios. Per-tenant backup splitting
/// is a future concern (probably handled at the cloud-DB layer rather than here).
/// </summary>
public sealed class DatabaseBackupJob
{
    public const string RecurringJobId = "database-backup-daily";
    public const string Cron = "0 2 * * *"; // 02:00 UTC daily

    private const int DaysToKeep = 3;

    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DatabaseBackupJob> _logger;

    public DatabaseBackupJob(
        AppDbContext context,
        IConfiguration config,
        IWebHostEnvironment environment,
        ILogger<DatabaseBackupJob> logger)
    {
        _context = context;
        _config = config;
        _environment = environment;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken ct)
    {
        var backupFolder = Path.Combine(_environment.ContentRootPath, "Backups");
        Directory.CreateDirectory(backupFolder);

        var dbName = ExtractDatabaseName(_config.GetConnectionString("DefaultConnection"));
        if (string.IsNullOrEmpty(dbName))
        {
            _logger.LogWarning("Could not derive Database= name from the connection string. Skipping backup.");
            return;
        }

        var fileName = $"{dbName}_{DateTime.UtcNow:yyyy-MM-dd_HHmm}.bak";
        var fullPath = Path.Combine(backupFolder, fileName);

        _logger.LogInformation("Starting database backup of {DbName} to {Path}", dbName, fullPath);

        var sql = $"BACKUP DATABASE [{dbName}] TO DISK = @p0 WITH FORMAT, MEDIANAME = 'NizamBackups', NAME = 'Full Backup of {dbName}';";
        await _context.Database.ExecuteSqlRawAsync(sql, new object[] { fullPath }, ct);

        _logger.LogInformation("Backup complete. Cleaning up files older than {Days} days.", DaysToKeep);
        CleanUpOldBackups(backupFolder);
    }

    /// <summary>
    /// Parses the database name from a SQL Server connection string. Accepts both
    /// <c>Database=</c> and <c>Initial Catalog=</c> keys. Returns null if neither found.
    /// </summary>
    private static string? ExtractDatabaseName(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return null;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = kv[0].Trim();
            if (key.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                return kv[1].Trim();
            }
        }
        return null;
    }

    private void CleanUpOldBackups(string folder)
    {
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*.bak"))
            {
                if (DateTime.UtcNow - File.GetCreationTimeUtc(file) > TimeSpan.FromDays(DaysToKeep))
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted old backup {File}", Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup cleanup.");
        }
    }
}
