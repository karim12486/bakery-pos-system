using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using System.Text.Json;

namespace BakeryPOS.API.Services
{
    public class ScheduledReportService : BackgroundService
    {
        private readonly ILogger<ScheduledReportService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string ReportStorageFolder = "GeneratedReports";

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
                    var moroccoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time");
                    var currentTimeInMorocco = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moroccoTimeZone);
                    var nextMidnightInMorocco = currentTimeInMorocco.Date.AddDays(1);
                    var delay = nextMidnightInMorocco - currentTimeInMorocco;

                    var nextRunTimeUtc = TimeZoneInfo.ConvertTimeToUtc(nextMidnightInMorocco, moroccoTimeZone);
                    _logger.LogInformation("Next report cycle will run at: {runTime} (UTC)", nextRunTimeUtc);

                    await Task.Delay(delay, stoppingToken);

                    _logger.LogInformation("Starting scheduled report generation...");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var reportService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
                        var pdfService = scope.ServiceProvider.GetRequiredService<IPdfGenerationService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        var reportDate = currentTimeInMorocco.Date;

                        Directory.CreateDirectory(ReportStorageFolder);

                        // --- Daily Report ---
                        var dailyReportDto = await reportService.GenerateDailySalesReportAsync(reportDate);
                        var dailyPdfBytes = pdfService.GenerateDailySalesReport(dailyReportDto);
                        string dailyReportFileName = $"Daily_Sales_{reportDate:yyyy-MM-dd}.pdf";
                        string dailyReportFilePath = Path.Combine(ReportStorageFolder, dailyReportFileName);
                        await File.WriteAllBytesAsync(dailyReportFilePath, dailyPdfBytes, stoppingToken);
                        await dbContext.Reports.AddAsync(new Report { Type = ReportType.DailySummary, PdfFilePath = dailyReportFilePath });
                        await notificationService.SendNotificationAsync($"Daily Sales Report for {reportDate:yyyy-MM-dd}", dailyReportFilePath);
                        _logger.LogInformation("Daily report PDF generated.");

                        // --- Special Product Report ---
                        var specialReportDto = await reportService.GenerateSpecialProductReportAsync(reportDate);
                        if (specialReportDto.ProductDetails.Any())
                        {
                            var specialPdfBytes = pdfService.GenerateSpecialProductReport(specialReportDto);
                            string specialReportFileName = $"Special_Products_{reportDate:yyyy-MM-dd}.pdf";
                            string specialReportFilePath = Path.Combine(ReportStorageFolder, specialReportFileName);
                            await File.WriteAllBytesAsync(specialReportFilePath, specialPdfBytes, stoppingToken);
                            await dbContext.Reports.AddAsync(new Report { Type = ReportType.ProductPerformance, PdfFilePath = specialReportFilePath });
                            await notificationService.SendNotificationAsync($"Special Product Report for {reportDate:yyyy-MM-dd}", specialReportFilePath);
                            _logger.LogInformation("Special Product report PDF generated.");
                        }

                        // --- Monthly Report (if it's the 1st of the month) ---
                        if (currentTimeInMorocco.Day == 1)
                        {
                            var previousMonth = currentTimeInMorocco.AddMonths(-1);
                            var monthlyReportDto = await reportService.GenerateMonthlySalesReportAsync(previousMonth.Year, previousMonth.Month);
                            var monthlyPdfBytes = pdfService.GenerateMonthlySalesReport(monthlyReportDto);
                            string monthlyReportFileName = $"Monthly_Sales_{previousMonth:yyyy-MM}.pdf";
                            string monthlyReportFilePath = Path.Combine(ReportStorageFolder, monthlyReportFileName);
                            await File.WriteAllBytesAsync(monthlyReportFilePath, monthlyPdfBytes, stoppingToken);
                            await dbContext.Reports.AddAsync(new Report { Type = ReportType.MonthlySummary, PdfFilePath = monthlyReportFilePath });
                            await notificationService.SendNotificationAsync($"Monthly Sales Report for {previousMonth:MMMM yyyy}", monthlyReportFilePath);
                            _logger.LogInformation("Monthly report PDF generated.");
                        }

                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("All generated reports have been saved to the database.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in the scheduled report service.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }
    }
}