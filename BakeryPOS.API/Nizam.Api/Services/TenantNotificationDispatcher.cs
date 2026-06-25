using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Nizam.Api.Services;

/// <summary>
/// Per-tenant <see cref="INotificationService"/>. Resolves the current tenant's
/// <c>MessagingConfig</c> and dispatches through whichever channel it selected:
/// <list type="bullet">
///   <item>Telegram — sends with the tenant's own bot token + chat id.</item>
///   <item>WhatsApp — config is stored but transport lands in Phase 4.6; sends are no-ops (logged).</item>
///   <item>None / inactive / no row — falls back to the global appsettings Telegram bot
///         (the legacy single-tenant behaviour), so existing deployments keep working.</item>
/// </list>
/// Callers (daily report job, reservation reminders) must already have a tenant in scope —
/// the background jobs set a tenant override before invoking this.
/// </summary>
public sealed class TenantNotificationDispatcher : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<TenantNotificationDispatcher> _logger;

    public TenantNotificationDispatcher(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<TenantNotificationDispatcher> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task SendNotificationAsync(string caption, string? filePath = null)
    {
        var config = await _context.MessagingConfigs.AsNoTracking().FirstOrDefaultAsync();

        if (config is null || !config.IsActive || config.Channel == MessagingChannel.None)
        {
            await SendTelegramAsync(
                _config["TelegramSettings:BotToken"],
                _config["TelegramSettings:ChatId"],
                caption, filePath);
            return;
        }

        switch (config.Channel)
        {
            case MessagingChannel.Telegram:
                await SendTelegramAsync(config.TelegramBotToken, config.TelegramChatId, caption, filePath);
                break;

            case MessagingChannel.WhatsApp:
                // Transport deferred to Phase 4.6 (WhatsApp Cloud API). Log so it's observable.
                _logger.LogInformation(
                    "WhatsApp notification suppressed (transport lands in Phase 4.6). Caption: {Caption}", caption);
                break;
        }
    }

    private async Task SendTelegramAsync(string? botToken, string? chatId, string caption, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Telegram notification skipped — bot token or chat id is missing.");
            return;
        }

        var httpClient = _httpClientFactory.CreateClient();

        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendDocument";
            using var form = new MultipartFormDataContent
            {
                { new StringContent(chatId), "chat_id" },
                { new StringContent(caption), "caption" }
            };
            await using var fileStream = File.OpenRead(filePath);
            form.Add(new StreamContent(fileStream), "document", Path.GetFileName(filePath));
            await httpClient.PostAsync(url, form);
        }
        else
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = JsonSerializer.Serialize(new { chat_id = chatId, text = caption });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await httpClient.PostAsync(url, content);
        }
    }
}
