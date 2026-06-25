using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs;

/// <summary>
/// The tenant's current messaging config as returned to the client. Secrets (bot token,
/// WhatsApp access token) are never echoed back — only a boolean flag indicating whether
/// one is stored, so the settings UI can show "configured" without exposing the value.
/// </summary>
public class MessagingConfigDto
{
    public MessagingChannel Channel { get; set; } = MessagingChannel.None;
    public bool IsActive { get; set; }

    // Telegram (non-secret echoed; token masked).
    public bool HasTelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }

    // WhatsApp (non-secret echoed; access token masked). Transport lands in Phase 4.6.
    public string? WhatsAppPhoneNumberId { get; set; }
    public bool HasWhatsAppAccessToken { get; set; }
    public string? WhatsAppRecipient { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Upsert of the tenant's messaging config. Secret fields are write-only and optional:
/// leave them null to keep the stored value, send a new value to replace it, send an empty
/// string to clear it.
/// </summary>
public class MessagingConfigUpdateDto
{
    [Required]
    public MessagingChannel Channel { get; set; } = MessagingChannel.None;

    public bool IsActive { get; set; } = true;

    // Telegram
    [StringLength(200)] public string? TelegramBotToken { get; set; }
    [StringLength(100)] public string? TelegramChatId { get; set; }

    // WhatsApp (Cloud API) — stored now, transport in Phase 4.6.
    [StringLength(100)] public string? WhatsAppPhoneNumberId { get; set; }
    [StringLength(500)] public string? WhatsAppAccessToken { get; set; }
    [StringLength(50)] public string? WhatsAppRecipient { get; set; }
}

/// <summary>Outcome of a "send me a test message" call from the settings screen.</summary>
public class MessagingTestResultDto
{
    public bool Sent { get; set; }
    public string Message { get; set; } = string.Empty;
}
