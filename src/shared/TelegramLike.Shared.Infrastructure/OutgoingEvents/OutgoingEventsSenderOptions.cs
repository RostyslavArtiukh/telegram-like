namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

public sealed class OutgoingEventsSenderOptions
{
    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 50;

    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// How long an already-published row is kept before Mongo's TTL monitor drops it.
    /// The queue marks rows sent instead of deleting them, which leaves a window to ask
    /// "what did we publish, and how long did it take" — but without an expiry the
    /// collection (and its index) would grow for the lifetime of the service.
    /// </summary>
    public int SentRetentionDays { get; set; } = 7;
}
