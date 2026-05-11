using BakeryPOS.API.Common.Tenancy;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

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
            _logger.LogInformation("Scheduled report service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Use UTC for the scheduling timer; the per-tenant generation block converts
                    // to that tenant's branch timezone for "what day are we reporting on".
                    var nowUtc = DateTime.UtcNow;
                    var nextMidnightUtc = nowUtc.Date.AddDays(1);
                    var delay = nextMidnightUtc - nowUtc;
                    _logger.LogInformation("Next scheduled report run at {RunTime} (UTC)", nextMidnightUtc);
                    await Task.Delay(delay, stoppingToken);

                    await GenerateForAllTenantsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled report run failed; retrying in 1 minute.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        /// <summary>
        /// Loads all tenants via a tenant-less scope, then opens a NEW scope per tenant with the
        /// tenant context set via <see cref="CurrentTenant.SetOverride"/>. Each per-tenant scope
        /// has its own <c>AppDbContext</c> whose global filter scopes to that tenant — so the
        /// per-tenant report queries are correctly isolated.
        /// </summary>
        private async Task GenerateForAllTenantsAsync(CancellationToken ct)
        {
            List<Tenant> tenants;
            using (var loaderScope = _scopeFactory.CreateScope())
            {
                var dbContext = loaderScope.ServiceProvider.GetRequiredService<AppDbContext>();
                // The Tenants table itself is NOT subject to the closed filter, but be explicit.
                tenants = await dbContext.Tenants.IgnoreQueryFilters().ToListAsync(ct);
            }

            _logger.LogInformation("Generating reports for {TenantCount} tenant(s)", tenants.Count);

            foreach (var tenant in tenants)
            {
                try
                {
                    await GenerateForTenantAsync(tenant, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Report generation failed for tenant {TenantId} ({Slug})", tenant.Id, tenant.Slug);
                    // Continue to next tenant — one bad tenant shouldn't break the run.
                }
            }
        }

        private async Task GenerateForTenantAsync(Tenant tenant, CancellationToken ct)
        {
            using var tenantScope = _scopeFactory.CreateScope();

            // Install tenant context for everything resolved from THIS scope (including AppDbContext).
            var currentTenant = (CurrentTenant)tenantScope.ServiceProvider.GetRequiredService<ICurrentTenant>();
            currentTenant.SetOverride(tenant.Id);

            var reportService = tenantScope.ServiceProvider.GetRequiredService<IReportGenerationService>();
            var pdfService = tenantScope.ServiceProvider.GetRequiredService<IPdfGenerationService>();
            var notificationService = tenantScope.ServiceProvider.GetRequiredService<INotificationService>();
            var dbContext = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Use the tenant's locale for receipt-style formatting; default to ar-EG if not parseable.
            CultureInfo culture;
            try { culture = new CultureInfo(tenant.Locale); }
            catch { culture = new CultureInfo("ar-EG"); }

            // "What day are we reporting on" is the tenant's "yesterday" in their local time.
            // For simplicity here we use UTC date; per-branch timezone-aware date math is M5.
            var reportDate = DateTime.UtcNow.Date.AddDays(-1);

            // Tenant-scoped report folder so two tenants don't fight over filenames.
            var reportsFolder = Path.Combine(ReportStorageFolder, $"tenant-{tenant.Id}");
            Directory.CreateDirectory(reportsFolder);

            // --- Daily ---
            var dailyDto = await reportService.GenerateDailySalesReportAsync(reportDate);
            var dailyPdf = pdfService.GenerateDailySalesReport(dailyDto);
            var dailyName = $"Daily_{reportDate:yyyy-MM-dd}.pdf";
            var dailyPath = Path.Combine(reportsFolder, dailyName);
            await File.WriteAllBytesAsync(dailyPath, dailyPdf, ct);
            await dbContext.Reports.AddAsync(new Report { Type = ReportType.DailySummary, PdfFilePath = dailyPath }, ct);
            await notificationService.SendNotificationAsync(FormatDailyReport(dailyDto, culture), dailyPath);

            // --- Special products ---
            var specialDto = await reportService.GenerateSpecialProductReportAsync(reportDate);
            if (specialDto.ProductDetails.Any())
            {
                var specialPdf = pdfService.GenerateSpecialProductReport(specialDto);
                var specialName = $"Special_{reportDate:yyyy-MM-dd}.pdf";
                var specialPath = Path.Combine(reportsFolder, specialName);
                await File.WriteAllBytesAsync(specialPath, specialPdf, ct);
                await dbContext.Reports.AddAsync(new Report { Type = ReportType.ProductPerformance, PdfFilePath = specialPath }, ct);
                await notificationService.SendNotificationAsync(FormatSpecialReport(specialDto, culture), specialPath);
            }

            // --- Monthly (1st of month) ---
            if (DateTime.UtcNow.Day == 1)
            {
                var previousMonth = DateTime.UtcNow.AddMonths(-1);
                var monthlyDto = await reportService.GenerateMonthlySalesReportAsync(previousMonth.Year, previousMonth.Month);
                var monthlyPdf = pdfService.GenerateMonthlySalesReport(monthlyDto);
                var monthlyName = $"Monthly_{previousMonth:yyyy-MM}.pdf";
                var monthlyPath = Path.Combine(reportsFolder, monthlyName);
                await File.WriteAllBytesAsync(monthlyPath, monthlyPdf, ct);
                await dbContext.Reports.AddAsync(new Report { Type = ReportType.MonthlySummary, PdfFilePath = monthlyPath }, ct);
                await notificationService.SendNotificationAsync(FormatMonthlyReport(monthlyDto, culture), monthlyPath);
            }

            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Reports generated for tenant {TenantId} ({Slug})", tenant.Id, tenant.Slug);
        }

        private static string FormatDailyReport(DailySalesReportDto report, CultureInfo culture)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📊 *Rapport Journalier - {report.ReportDate.ToString("d", culture)}*");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"💰 *Total Ventes :* {report.GrandTotalSalesValue.ToString("C", culture)}");
            sb.AppendLine($"🧾 *Transactions :* {report.GrandTotalTransactions}");
            sb.AppendLine("------------------------------------");
            if (report.SalesByCashier.Any())
            {
                sb.AppendLine("*Ventes par Caissier :*");
                foreach (var c in report.SalesByCashier)
                    sb.AppendLine($"  👤 {c.CashierName}: {c.TotalSalesValue.ToString("C", culture)} ({c.TotalTransactions} trans.)");
            }
            else sb.AppendLine("Aucune vente enregistrée ce jour.");
            return sb.ToString();
        }

        private static string FormatSpecialReport(SpecialProductReportDto report, CultureInfo culture)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"⭐ *Produits Spéciaux - {report.ReportDate.ToString("d", culture)}*");
            sb.AppendLine("------------------------------------");
            foreach (var item in report.ProductDetails)
            {
                sb.AppendLine($"🔹 *{item.ProductName}*");
                sb.AppendLine($"   ➕ Ajouté : {item.QuantityAdded}");
                sb.AppendLine($"   🛒 Vendu : {item.QuantitySold}");
                sb.AppendLine($"   💵 Revenu : {item.TotalRevenue.ToString("C", culture)}");
                sb.AppendLine($"   📈 Bénéfice : {item.Profit.ToString("C", culture)}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string FormatMonthlyReport(MonthlySalesReportDto report, CultureInfo culture)
        {
            var sb = new StringBuilder();
            var monthName = new DateTime(report.Year, report.Month, 1).ToString("MMMM yyyy", culture);
            sb.AppendLine($"📅 *Rapport Mensuel - {monthName}*");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"💰 *Total Ventes :* {report.GrandTotalSalesValue.ToString("C", culture)}");
            sb.AppendLine($"🧾 *Total Transactions :* {report.GrandTotalTransactions}");
            sb.AppendLine($"📉 *Total Remises :* {report.GrandTotalDiscountAmount.ToString("C", culture)}");
            sb.AppendLine($"🛒 *Panier Moyen :* {report.AverageTransactionValue.ToString("C", culture)}");
            return sb.ToString();
        }
    }
}
