namespace TelegramLike.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 50;
}
