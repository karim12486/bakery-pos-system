using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BakeryPOS.API.Services
{
    public class ScheduledReportService : BackgroundService
    {
        private readonly ILogger<ScheduledReportService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string ReportStorageFolder = "GeneratedReports";

        // Ensure we use French formatting for dates/currency in the text messages
        private readonly CultureInfo _culture = new CultureInfo("fr-MA");

        public ScheduledReportService(ILogger<ScheduledReportService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service de rapports programmés démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var moroccoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time");
                    var currentTimeInMorocco = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moroccoTimeZone);
                    var nextMidnightInMorocco = currentTimeInMorocco.Date.AddDays(1);
                    var delay = nextMidnightInMorocco - currentTimeInMorocco;

                    var nextRunTimeUtc = TimeZoneInfo.ConvertTimeToUtc(nextMidnightInMorocco, moroccoTimeZone);
                    _logger.LogInformation("Prochain rapport prévu à : {runTime} (UTC)", nextRunTimeUtc);

                    await Task.Delay(delay, stoppingToken);

                    _logger.LogInformation("Génération des rapports en cours...");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var reportService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
                        var pdfService = scope.ServiceProvider.GetRequiredService<IPdfGenerationService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        var reportDate = currentTimeInMorocco.Date;
                        Directory.CreateDirectory(ReportStorageFolder);

                        // --- 1. Rapport Journalier ---
                        var dailyReportDto = await reportService.GenerateDailySalesReportAsync(reportDate);
                        var dailyPdfBytes = pdfService.GenerateDailySalesReport(dailyReportDto);
                        string dailyReportFileName = $"Rapport_Journalier_{reportDate:yyyy-MM-dd}.pdf";
                        string dailyReportFilePath = Path.Combine(ReportStorageFolder, dailyReportFileName);
                        await File.WriteAllBytesAsync(dailyReportFilePath, dailyPdfBytes, stoppingToken);

                        await dbContext.Reports.AddAsync(new Report { Type = ReportType.DailySummary, PdfFilePath = dailyReportFilePath });

                        var dailyMessage = FormatDailyReportForTelegram(dailyReportDto);
                        await notificationService.SendNotificationAsync(dailyMessage, dailyReportFilePath);

                        // --- 2. Rapport Produits Spéciaux ---
                        var specialReportDto = await reportService.GenerateSpecialProductReportAsync(reportDate);
                        if (specialReportDto.ProductDetails.Any())
                        {
                            var specialPdfBytes = pdfService.GenerateSpecialProductReport(specialReportDto);
                            string specialReportFileName = $"Produits_Speciaux_{reportDate:yyyy-MM-dd}.pdf";
                            string specialReportFilePath = Path.Combine(ReportStorageFolder, specialReportFileName);
                            await File.WriteAllBytesAsync(specialReportFilePath, specialPdfBytes, stoppingToken);

                            await dbContext.Reports.AddAsync(new Report { Type = ReportType.ProductPerformance, PdfFilePath = specialReportFilePath });

                            var specialMessage = FormatSpecialProductReportForTelegram(specialReportDto);
                            await notificationService.SendNotificationAsync(specialMessage, specialReportFilePath);
                        }

                        // --- 3. Rapport Mensuel (le 1er du mois) ---
                        if (currentTimeInMorocco.Day == 1)
                        {
                            var previousMonth = currentTimeInMorocco.AddMonths(-1);
                            var monthlyReportDto = await reportService.GenerateMonthlySalesReportAsync(previousMonth.Year, previousMonth.Month);
                            var monthlyPdfBytes = pdfService.GenerateMonthlySalesReport(monthlyReportDto);
                            string monthlyReportFileName = $"Rapport_Mensuel_{previousMonth:yyyy-MM}.pdf";
                            string monthlyReportFilePath = Path.Combine(ReportStorageFolder, monthlyReportFileName);
                            await File.WriteAllBytesAsync(monthlyReportFilePath, monthlyPdfBytes, stoppingToken);

                            await dbContext.Reports.AddAsync(new Report { Type = ReportType.MonthlySummary, PdfFilePath = monthlyReportFilePath });

                            var monthlyMessage = FormatMonthlyReportForTelegram(monthlyReportDto);
                            await notificationService.SendNotificationAsync(monthlyMessage, monthlyReportFilePath);
                        }

                        await dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur dans le service de rapports.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private string FormatDailyReportForTelegram(DailySalesReportDto report)
        {
            var sb = new StringBuilder();
            // "d" format in French culture gives "22/11/2025"
            sb.AppendLine($"📊 *Rapport Journalier - {report.ReportDate.ToString("d", _culture)}*");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"💰 *Total Ventes :* {report.GrandTotalSalesValue.ToString("C", _culture)}");
            sb.AppendLine($"🧾 *Transactions :* {report.GrandTotalTransactions}");
            sb.AppendLine("------------------------------------");

            if (report.SalesByCashier.Any())
            {
                sb.AppendLine("*Ventes par Caissier :*");
                foreach (var cashierSale in report.SalesByCashier)
                {
                    sb.AppendLine($"  👤 {cashierSale.CashierName}: {cashierSale.TotalSalesValue.ToString("C", _culture)} ({cashierSale.TotalTransactions} trans.)");
                }
            }
            else
            {
                sb.AppendLine("Aucune vente enregistrée ce jour.");
            }

            return sb.ToString();
        }

        private string FormatSpecialProductReportForTelegram(SpecialProductReportDto report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"⭐ *Produits Spéciaux - {report.ReportDate.ToString("d", _culture)}*");
            sb.AppendLine("------------------------------------");

            foreach (var item in report.ProductDetails)
            {
                sb.AppendLine($"🔹 *{item.ProductName}*");
                sb.AppendLine($"   ➕ Ajouté : {item.QuantityAdded}");
                sb.AppendLine($"   🛒 Vendu : {item.QuantitySold}");
                sb.AppendLine($"   💵 Revenu : {item.TotalRevenue.ToString("C", _culture)}");
                sb.AppendLine($"   📈 Bénéfice : {item.Profit.ToString("C", _culture)}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string FormatMonthlyReportForTelegram(MonthlySalesReportDto report)
        {
            var sb = new StringBuilder();
            var monthName = new DateTime(report.Year, report.Month, 1).ToString("MMMM yyyy", _culture);
            sb.AppendLine($"📅 *Rapport Mensuel - {monthName}*");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"💰 *Total Ventes :* {report.GrandTotalSalesValue.ToString("C", _culture)}");
            sb.AppendLine($"🧾 *Total Transactions :* {report.GrandTotalTransactions}");
            sb.AppendLine($"📉 *Total Remises :* {report.GrandTotalDiscountAmount.ToString("C", _culture)}");
            sb.AppendLine($"🛒 *Panier Moyen :* {report.AverageTransactionValue.ToString("C", _culture)}");

            return sb.ToString();
        }
    }
}