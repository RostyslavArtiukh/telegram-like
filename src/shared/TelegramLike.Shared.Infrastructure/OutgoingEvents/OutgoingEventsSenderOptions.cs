namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

public sealed class OutgoingEventsSenderOptions
{
    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 50;

    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// How many events of a claimed batch are published at once.
    /// </summary>
    /// <remarks>
    /// Publishing a batch one event at a time made a replica's throughput the reciprocal of a
    /// single broker round-trip, no matter how much work was waiting ([TL-125]). Concurrency
    /// costs the order of events <i>within</i> a batch — which nothing here relies on: read
    /// models resolve last-writer-wins by <c>OccurredAt</c>, notifications deduplicate by
    /// (recipient, source event), and relay pushes are id-only signals the UI refetches. Two
    /// replicas already published concurrently anyway. Set it to 1 for strict in-batch order.
    /// </remarks>
    public int PublishConcurrency { get; set; } = 4;

    /// <summary>
    /// How long an already-published row is kept before Mongo's TTL monitor drops it.
    /// The queue marks rows sent instead of deleting them, which leaves a window to ask
    /// "what did we publish, and how long did it take" — but without an expiry the
    /// collection (and its index) would grow for the lifetime of the service.
    /// </summary>
    public int SentRetentionDays { get; set; } = 7;
}
