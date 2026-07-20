namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

/// <summary>
/// A point-in-time snapshot of how far behind the outgoing-events queue is.
/// Sampled by <see cref="OutboxBacklogPoller"/> and published as gauges by
/// <see cref="OutboxMetrics"/>.
/// </summary>
/// <param name="PendingCount">Rows waiting to be published (not sent, not dead-lettered).</param>
/// <param name="DeadLetteredCount">Rows that exhausted their retries and will never be sent.</param>
/// <param name="OldestPendingAgeSeconds">
/// Age of the oldest pending row. This is the real lag signal: a pending count of 50 is
/// harmless if it drains every second, but a 30s-old head means events are stuck.
/// </param>
public sealed record OutboxBacklog(
    long PendingCount,
    long DeadLetteredCount,
    double OldestPendingAgeSeconds)
{
    public static readonly OutboxBacklog Empty = new(0, 0, 0);
}
