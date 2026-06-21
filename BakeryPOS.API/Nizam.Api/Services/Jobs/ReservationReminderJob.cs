using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services.Jobs;

/// <summary>
/// Hangfire-scheduled reservation housekeeping, per tenant:
///   1. Sends a reminder for Booked/Confirmed reservations whose slot is within the next
///      <see cref="ReminderWindow"/> and that haven't been reminded yet.
///   2. Auto-marks NoShow for reservations whose slot lapsed more than
///      <see cref="NoShowGrace"/> ago and that were never seated/cancelled.
///
/// <para>The reminder currently goes through <see cref="INotificationService"/> (Telegram).
/// When the WhatsApp Cloud API integration lands (Phase 4.6), swap the notifier — the job
/// logic is channel-agnostic.</para>
/// </summary>
public sealed class ReservationReminderJob
{
    public const string RecurringJobId = "reservation-reminders-per-tenant";
    public const string Cron = "*/15 * * * *"; // every 15 minutes

    private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(2);
    private static readonly TimeSpan NoShowGrace = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationReminderJob> _logger;

    public ReservationReminderJob(IServiceScopeFactory scopeFactory, ILogger<ReservationReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(CancellationToken ct)
    {
        List<Tenant> tenants;
        using (var loader = _scopeFactory.CreateScope())
        {
            var db = loader.ServiceProvider.GetRequiredService<AppDbContext>();
            tenants = await db.Tenants.IgnoreQueryFilters().ToListAsync(ct);
        }

        foreach (var tenant in tenants)
        {
            try { await ProcessTenantAsync(tenant.Id, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservation reminders failed for tenant {TenantId}", tenant.Id);
            }
        }
    }

    private async Task ProcessTenantAsync(int tenantId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var currentTenant = (CurrentTenant)scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        currentTenant.SetOverride(tenantId);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var reminderCutoff = now.Add(ReminderWindow);
        var noShowCutoff = now.Subtract(NoShowGrace);

        // 1. Reminders — upcoming, not yet reminded.
        var dueReminders = await db.Reservations
            .Where(r => (r.Status == ReservationStatus.Booked || r.Status == ReservationStatus.Confirmed)
                        && r.ReminderSentAt == null
                        && r.ReservedFor > now
                        && r.ReservedFor <= reminderCutoff)
            .ToListAsync(ct);

        foreach (var r in dueReminders)
        {
            await notifier.SendNotificationAsync(
                $"⏰ Reminder: {r.CustomerName} (party of {r.PartySize}) at {r.ReservedFor:HH:mm} today.");
            r.ReminderSentAt = now;
        }

        // 2. No-shows — slot lapsed, never seated/cancelled.
        var noShows = await db.Reservations
            .Where(r => (r.Status == ReservationStatus.Booked || r.Status == ReservationStatus.Confirmed)
                        && r.ReservedFor < noShowCutoff)
            .ToListAsync(ct);
        foreach (var r in noShows) r.Status = ReservationStatus.NoShow;

        if (dueReminders.Count > 0 || noShows.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Tenant {TenantId}: {Reminders} reminder(s), {NoShows} no-show(s)",
                tenantId, dueReminders.Count, noShows.Count);
        }
    }
}
