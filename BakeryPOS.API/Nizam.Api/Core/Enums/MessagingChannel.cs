namespace Nizam.Api.Core.Enums;

/// <summary>Which outbound channel a tenant uses for notifications.</summary>
public enum MessagingChannel
{
    /// <summary>No notifications.</summary>
    None,

    /// <summary>Telegram bot (tenant supplies bot token + chat id).</summary>
    Telegram,

    /// <summary>WhatsApp Cloud API (transport lands in Phase 4.6; config stored now).</summary>
    WhatsApp
}
