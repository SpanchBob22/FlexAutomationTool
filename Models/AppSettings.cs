using System;

namespace FlexAutomator.Models;

public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? YouTubeApiKey { get; set; }

    public string? TMDbApiKey { get; set; }

    public string? TelegramBotToken { get; set; }

    public long? TelegramChatId { get; set; }

    public string? PairingCode { get; set; }

    public bool IsTelegramBotEnabled { get; set; } = false;
}