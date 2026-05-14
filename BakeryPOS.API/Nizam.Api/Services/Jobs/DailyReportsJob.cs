using System.Globalization;
using System.Text;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services.Jobs;

/// <summary>
/// Hangfire-scheduled per-tenant daily/monthly reports. Replaces the legacy
/// <c>ScheduledReportService : BackgroundService</c>. Iterates every tenant, sets the
/// tenant context for THAT scope, generates the daily PDFs, persists them, and pushes
/// the summary to Telegram (or whatever <see cref="INotificationService"/> resolves to).
///
/// <para>Why a Hangfire job vs the old BackgroundService:</para>
/// <list type="bullet">
///   <item>Survives process restarts — Hangfire persists schedule state in SQL.</item>
///   <item>Automatic retries on transient failure (DB blip, Telegram outage).</item>
///   <item>Visible in /hangfire dashboard with last-run + next-run + duration.</item>
///   <item>Scales horizontally — multiple servers won't double-run the same recurring job.</item>
/// </list>
/// </summary>
public sealed class DailyReportsJob
{
    public const string RecurringJobId = "daily-reports-per-tenant";
    public const string Cron = "5 0 * * *"; // 00:05 UTC daily — runs once after midnight UTC

    private const string ReportStorageFolder = "GeneratedReports";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyReportsJob> _logger;

    public DailyReportsJob(IServiceScopeFactory scopeFactory, ILogger<DailyReportsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 300, 1800 })]
    [DisableConcurrentExecution(timeoutInSeconds: 1200)]
    public async Task RunAsync(CancellationToken ct)
    {
        List<Tenant> tenants;
        using (var loader = _scopeFactory.CreateScope())
        {
            var db = loader.ServiceProvider.GetRequiredService<AppDbContext>();
            tenants = await db.Tenants.IgnoreQueryFilters().ToListAsync(ct);
        }

        _logger.LogInformation("Generating daily reports for {TenantCount} tenant(s)", tenants.Count);

        foreach (var tenant in tenants)
        {
            try
            {
                await GenerateForTenantAsync(tenant, ct);
            }
            catch (Exception ex)
            {
                // Don't kill the whole job because one tenant failed. Hangfire would retry the
                // whole run; we'd rather log + continue + investigate via /hangfire.
                _logger.LogError(ex, "Report generation failed for tenant {TenantId} ({Slug})", tenant.Id, tenant.Slug);
            }
        }
    }

    private async Task GenerateForTenantAsync(Tenant tenant, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var currentTenant = (CurrentTenant)scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        currentTenant.SetOverride(tenant.Id);

        var reportService = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
        var pdfService = scope.ServiceProvider.GetRequiredService<IPdfGenerationService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        CultureInfo culture;
        try { culture = new CultureInfo(tenant.Locale); }
        catch { culture = new CultureInfo("ar-EG"); }

        var reportDate = DateTime.UtcNow.Date.AddDays(-1); // "yesterday" UTC

        var reportsFolder = Path.Combine(env.ContentRootPath, ReportStorageFolder, $"tenant-{tenant.Id}");
        Directory.CreateDirectory(reportsFolder);

        // Daily
        var dailyDto = await reportService.GenerateDailySalesReportAsync(reportDate);
        var dailyPdf = pdfService.GenerateDailySalesReport(dailyDto);
        var dailyPath = Path.Combine(reportsFolder, $"Daily_{reportDate:yyyy-MM-dd}.pdf");
        await File.WriteAllBytesAsync(dailyPath, dailyPdf, ct);
        await db.Reports.AddAsync(new Report { Type = ReportType.DailySummary, PdfFilePath = dailyPath }, ct);
        await notificationService.SendNotificationAsync(FormatDaily(dailyDto, culture), dailyPath);

        // Special products
        var specialDto = await reportService.GenerateSpecialProductReportAsync(reportDate);
        if (specialDto.ProductDetails.Any())
        {
            var specialPdf = pdfService.GenerateSpecialProductReport(specialDto);
            var specialPath = Path.Combine(reportsFolder, $"Special_{reportDate:yyyy-MM-dd}.pdf");
            await File.WriteAllBytesAsync(specialPath, specialPdf, ct);
            await db.Reports.AddAsync(new Report { Type = ReportType.ProductPerformance, PdfFilePath = specialPath }, ct);
            await notificationService.SendNotificationAsync(FormatSpecial(specialDto, culture), specialPath);
        }

        // Monthly (1st of month only)
        if (DateTime.UtcNow.Day == 1)
        {
            var previousMonth = DateTime.UtcNow.AddMonths(-1);
            var monthlyDto = await reportService.GenerateMonthlySalesReportAsync(previousMonth.Year, previousMonth.Month);
            var monthlyPdf = pdfService.GenerateMonthlySalesReport(monthlyDto);
            var monthlyPath = Path.Combine(reportsFolder, $"Monthly_{previousMonth:yyyy-MM}.pdf");
            await File.WriteAllBytesAsync(monthlyPath, monthlyPdf, ct);
            await db.Reports.AddAsync(new Report { Type = ReportType.MonthlySummary, PdfFilePath = monthlyPath }, ct);
            await notificationService.SendNotificationAsync(FormatMonthly(monthlyDto, culture), monthlyPath);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Reports generated for tenant {TenantId} ({Slug})", tenant.Id, tenant.Slug);
    }

    private static string FormatDaily(DailySalesReportDto r, CultureInfo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📊 *Rapport Journalier - {r.ReportDate.ToString("d", c)}*");
        sb.AppendLine("------------------------------------");
        sb.AppendLine($"💰 *Total Ventes :* {r.GrandTotalSalesValue.ToString("C", c)}");
        sb.AppendLine($"🧾 *Transactions :* {r.GrandTotalTransactions}");
        sb.AppendLine("------------------------------------");
        if (r.SalesByCashier.Any())
        {
            sb.AppendLine("*Ventes par Caissier :*");
            foreach (var x in r.SalesByCashier)
                sb.AppendLine($"  👤 {x.CashierName}: {x.TotalSalesValue.ToString("C", c)} ({x.TotalTransactions} trans.)");
        }
        else sb.AppendLine("Aucune vente enregistrée ce jour.");
        return sb.ToString();
    }

    private static string FormatSpecial(SpecialProductReportDto r, CultureInfo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"⭐ *Produits Spéciaux - {r.ReportDate.ToString("d", c)}*");
        sb.AppendLine("------------------------------------");
        foreach (var item in r.ProductDetails)
        {
            sb.AppendLine($"🔹 *{item.ProductName}*");
            sb.AppendLine($"   ➕ Ajouté : {item.QuantityAdded}");
            sb.AppendLine($"   🛒 Vendu : {item.QuantitySold}");
            sb.AppendLine($"   💵 Revenu : {item.TotalRevenue.ToString("C", c)}");
            sb.AppendLine($"   📈 Bénéfice : {item.Profit.ToString("C", c)}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatMonthly(MonthlySalesReportDto r, CultureInfo c)
    {
        var sb = new StringBuilder();
        var monthName = new DateTime(r.Year, r.Month, 1).ToString("MMMM yyyy", c);
        sb.AppendLine($"📅 *Rapport Mensuel - {monthName}*");
        sb.AppendLine("------------------------------------");
        sb.AppendLine($"💰 *Total Ventes :* {r.GrandTotalSalesValue.ToString("C", c)}");
        sb.AppendLine($"🧾 *Total Transactions :* {r.GrandTotalTransactions}");
        sb.AppendLine($"📉 *Total Remises :* {r.GrandTotalDiscountAmount.ToString("C", c)}");
        sb.AppendLine($"🛒 *Panier Moyen :* {r.AverageTransactionValue.ToString("C", c)}");
        return sb.ToString();
    }
}
