using BakeryPOS.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Services
{
    public class DatabaseBackupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseBackupService> _logger;

        // Configuration
        private const string DbName = "BakeryPOS_DB";
        // Backup to a "Backups" folder next to the app
        private readonly string _backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        private const int BackupHour = 2; // time in 24H format (2 = 2 AM)
        private const int DaysToKeep = 3; // Delete backups older than 3 days

        public DatabaseBackupService(IServiceProvider serviceProvider, ILogger<DatabaseBackupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Database Backup Service started.");

            // Ensure backup directory exists
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate time until next backup (23:00 local time)
                    var now = DateTime.Now;
                    var nextRun = now.Date.AddHours(BackupHour);
                    if (now > nextRun)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    var delay = nextRun - now;
                    _logger.LogInformation($"Next database backup scheduled for: {nextRun}");

                    // Wait...
                    await Task.Delay(delay, stoppingToken);
                    //await Task.Delay(5000, stoppingToken);

                    // --- START BACKUP ---
                    _logger.LogInformation("Starting database backup...");

                    string fileName = $"{DbName}_{DateTime.Now:yyyy-MM-dd_HHmm}.bak";
                    string fullPath = Path.Combine(_backupFolder, fileName);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Execute Raw SQL command to tell SQL Server to backup
                        // FORMAT: BACKUP DATABASE [Name] TO DISK = 'Path'
                        var sqlCommand = $"BACKUP DATABASE [{DbName}] TO DISK = '{fullPath}' WITH FORMAT, MEDIANAME = 'Z_SQLServerBackups', NAME = 'Full Backup of {DbName}';";

                        // We use ExecuteSqlRawAsync because this is a DDL command, not a query
                        await dbContext.Database.ExecuteSqlRawAsync(sqlCommand, stoppingToken);
                    }

                    _logger.LogInformation($"Database backup successful: {fullPath}");

                    // --- CLEANUP OLD FILES ---
                    CleanUpOldBackups();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical Error: Database backup failed.");
                    // Wait 1 hour before retrying if it fails, to avoid rapid loops
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private void CleanUpOldBackups()
        {
            try
            {
                var files = Directory.GetFiles(_backupFolder, "*.bak");
                foreach (var file in files)
                {
                    var creationTime = File.GetCreationTime(file);
                    if (DateTime.Now - creationTime > TimeSpan.FromDays(DaysToKeep))
                    {
                        File.Delete(file);
                        _logger.LogInformation($"Deleted old backup: {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup cleanup.");
            }
        }
    }
}