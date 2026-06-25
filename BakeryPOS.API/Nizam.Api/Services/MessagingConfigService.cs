using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IMessagingConfigService
{
    /// <summary>The current tenant's messaging config (secrets masked). Never null — returns
    /// an unconfigured (Channel = None) default when the tenant has no row yet.</summary>
    Task<MessagingConfigDto> GetAsync(CancellationToken ct);

    /// <summary>Creates or updates the current tenant's config. Secret fields use keep/clear/set
    /// semantics (null = keep stored value, empty = clear, value = replace).</summary>
    Task<MessagingConfigDto> UpdateAsync(MessagingConfigUpdateDto dto, CancellationToken ct);

    /// <summary>Sends a test notification through the tenant's configured channel.</summary>
    Task<MessagingTestResultDto> SendTestAsync(CancellationToken ct);
}

public sealed class MessagingConfigService : IMessagingConfigService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notifier;

    public MessagingConfigService(AppDbContext context, INotificationService notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    public async Task<MessagingConfigDto> GetAsync(CancellationToken ct)
    {
        var config = await _context.MessagingConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return config is null ? new MessagingConfigDto() : ToDto(config);
    }

    public async Task<MessagingConfigDto> UpdateAsync(MessagingConfigUpdateDto dto, CancellationToken ct)
    {
        var config = await _context.MessagingConfigs.FirstOrDefaultAsync(ct);
        if (config is null)
        {
            config = new MessagingConfig();
            _context.MessagingConfigs.Add(config);
        }

        config.Channel = dto.Channel;
        config.IsActive = dto.IsActive;

        // Non-secret fields: replace outright.
        config.TelegramChatId = dto.TelegramChatId;
        config.WhatsAppPhoneNumberId = dto.WhatsAppPhoneNumberId;
        config.WhatsAppRecipient = dto.WhatsAppRecipient;

        // Secret fields: null = keep, empty = clear, value = replace.
        config.TelegramBotToken = ApplySecret(config.TelegramBotToken, dto.TelegramBotToken);
        config.WhatsAppAccessToken = ApplySecret(config.WhatsAppAccessToken, dto.WhatsAppAccessToken);

        config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToDto(config);
    }

    public async Task<MessagingTestResultDto> SendTestAsync(CancellationToken ct)
    {
        var config = await _context.MessagingConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (config is null || config.Channel == MessagingChannel.None || !config.IsActive)
            return new MessagingTestResultDto { Sent = false, Message = "No active messaging channel is configured." };

        await _notifier.SendNotificationAsync("✅ NIZAM test notification — your messaging channel is working.");
        return new MessagingTestResultDto { Sent = true, Message = $"Test message dispatched via {config.Channel}." };
    }

    private static string? ApplySecret(string? current, string? incoming)
    {
        if (incoming is null) return current;                  // keep
        if (incoming.Length == 0) return null;                 // clear
        return incoming;                                       // replace
    }

    private static MessagingConfigDto ToDto(MessagingConfig c) => new()
    {
        Channel = c.Channel,
        IsActive = c.IsActive,
        HasTelegramBotToken = !string.IsNullOrEmpty(c.TelegramBotToken),
        TelegramChatId = c.TelegramChatId,
        WhatsAppPhoneNumberId = c.WhatsAppPhoneNumberId,
        HasWhatsAppAccessToken = !string.IsNullOrEmpty(c.WhatsAppAccessToken),
        WhatsAppRecipient = c.WhatsAppRecipient,
        UpdatedAt = c.UpdatedAt
    };
}
