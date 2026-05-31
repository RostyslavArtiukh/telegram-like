namespace TelegramLike.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 50;

    // After this many failed publish attempts the message is moved to the dead-letter
    // state (DeadLetteredAt set, excluded from GetPendingAsync) so a poison message
    // cannot block the publisher forever. Manual intervention is required to replay it.
    public int MaxRetries { get; set; } = 5;
}
