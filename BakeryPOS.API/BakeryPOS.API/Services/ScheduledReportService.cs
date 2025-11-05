using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using System.Text;
using System.Text.Json;

namespace BakeryPOS.API.Services
{
    public class ScheduledReportService : BackgroundService
    {
        private readonly ILogger<ScheduledReportService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // The time of day (in UTC) to run the report. 21:00 UTC is 11 PM in Cairo (UTC+2).
        private const int ReportHourUtc = 23;

        public ScheduledReportService(ILogger<ScheduledReportService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Report Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Get the Morocco time zone information
                    var moroccoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time");

                    // 2. Get the current time in Morocco
                    var currentTimeInMorocco = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moroccoTimeZone);

                    // 3. Calculate the next midnight in Morocco
                    var nextMidnightInMorocco = currentTimeInMorocco.Date.AddDays(1);

                    // 4. Calculate the delay until that time
                    var delay = nextMidnightInMorocco - currentTimeInMorocco;

                    // Convert next run time back to UTC for logging
                    var nextRunTimeUtc = TimeZoneInfo.ConvertTimeToUtc(nextMidnightInMorocco, moroccoTimeZone);
                    _logger.LogInformation("Next daily report will run at: {runTime} (UTC)", nextRunTimeUtc);

                    // Wait for the calculated delay
                    await Task.Delay(delay, stoppingToken);

                    // --- It's time to run the report! ---
                    _logger.LogInformation("Generating daily report...");

                    // Create a new scope to get our scoped services
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var reportService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Generate the report for the day that just ended in Morocco
                        var reportDate = currentTimeInMorocco.Date;
                        var reportDto = await reportService.GenerateDailySalesReportAsync(reportDate);

                        // Save the report to the database
                        var newReport = new Report
                        {
                            Type = ReportType.DailySummary,
                            ReportDataJson = JsonSerializer.Serialize(reportDto)
                        };
                        await dbContext.Reports.AddAsync(newReport);
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Daily report for {reportDate} saved to database.", reportDate.ToShortDateString());

                        // Format and send the notification
                        var message = FormatReportForTelegram(reportDto);
                        await notificationService.SendNotificationAsync(message);
                        _logger.LogInformation("Daily report sent via notification.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in the scheduled report service.");
                    // Wait for a minute before retrying to avoid fast loop on error
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private string FormatReportForTelegram(DTOs.DailySalesReportDto report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"*Daily Sales Report for {report.ReportDate:yyyy-MM-dd}*");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"*Total Sales:* {report.GrandTotalSalesValue:C}");
            sb.AppendLine($"*Total Transactions:* {report.GrandTotalTransactions}");
            sb.AppendLine("------------------------------------");
            sb.AppendLine("*Sales by Cashier:*");

            foreach (var cashierSale in report.SalesByCashier)
            {
                sb.AppendLine($"  - {cashierSale.CashierName}: {cashierSale.TotalSalesValue:C} ({cashierSale.TotalTransactions} transactions)");
            }

            return sb.ToString();
        }
    }
}