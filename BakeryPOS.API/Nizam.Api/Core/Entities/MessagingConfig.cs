using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities;

/// <summary>
/// Per-tenant messaging configuration — which channel the tenant uses for outbound
/// notifications (reports, reservation reminders) and the channel's credentials. One row per
/// tenant. Replaces the single global Telegram bot from appsettings.
///
/// <para>Telegram is live; WhatsApp config is stored now but its transport lands in Phase 4.6
/// (WhatsApp Cloud API). Until then a WhatsApp-configured tenant's sends are no-ops (logged).</para>
/// </summary>
public class MessagingConfig
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public MessagingChannel Channel { get; set; } = MessagingChannel.None;

    // Telegram
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }

    // WhatsApp (Cloud API) — transport deferred to 4.6
    public string? WhatsAppPhoneNumberId { get; set; }
    public string? WhatsAppAccessToken { get; set; }
    public string? WhatsAppRecipient { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
