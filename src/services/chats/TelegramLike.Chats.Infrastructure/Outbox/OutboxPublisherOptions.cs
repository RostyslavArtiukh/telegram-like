namespace TelegramLike.Chats.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 50;

    public int MaxRetries { get; set; } = 5;
}
