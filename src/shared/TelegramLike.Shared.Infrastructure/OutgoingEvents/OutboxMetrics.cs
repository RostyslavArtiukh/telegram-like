using System.Diagnostics.Metrics;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Metrics for the transactional outbox, shared by every service that uses it.
/// Counters are recorded by <see cref="OutgoingEventsSender"/> as it drains the queue;
/// the gauges report the latest snapshot taken by <see cref="OutboxBacklogPoller"/>.
/// Prometheus' own <c>job</c> label already separates services, so nothing here is
/// tagged with a service name.
/// </summary>
public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "TelegramLike.Outbox";

    /// <summary>
    /// Bucket boundaries for <c>telegramlike.outbox.publish_delay</c>, in seconds.
    /// The SDK's default buckets (0, 5, 10, 25, … 10000) assume milliseconds, so every
    /// real delay lands in the first bucket and <c>histogram_quantile</c> can only answer
    /// "somewhere under 5s". These straddle the 2s default poll interval instead — the
    /// floor this delay can't go below — and keep coarse buckets above it for stalls.
    /// Services apply them with a metrics View; a histogram's buckets can't be set on
    /// the instrument itself.
    /// </summary>
    public static readonly double[] PublishDelayBucketsSeconds =
        [0.1, 0.25, 0.5, 1, 2, 3, 5, 10, 30, 60];

    private readonly Meter _meter;
    private readonly Counter<long> _published;
    private readonly Counter<long> _publishFailures;
    private readonly Counter<long> _deadLettered;
    private readonly Histogram<double> _publishDelay;

    // Written by the poller thread, read by the metrics-collection thread. Reference
    // assignment is atomic and the record is immutable, so a reader always sees one
    // coherent snapshot rather than a half-updated struct.
    private volatile OutboxBacklog _backlog = OutboxBacklog.Empty;

    public OutboxMetrics()
    {
        _meter = new Meter(MeterName);

        _published = _meter.CreateCounter<long>(
            "telegramlike.outbox.published",
            unit: "{event}",
            description: "Integration events successfully published to the broker.");

        _publishFailures = _meter.CreateCounter<long>(
            "telegramlike.outbox.publish_failures",
            unit: "{event}",
            description: "Failed publish attempts (the event is retried).");

        _deadLettered = _meter.CreateCounter<long>(
            "telegramlike.outbox.dead_lettered",
            unit: "{event}",
            description: "Events that exhausted their retries and were given up on.");

        // End-to-end outbox latency: how long an event sat between being written inside
        // the business transaction and reaching the broker. Bounded below by the sender's
        // poll interval, so expect a floor around OutgoingEvents:PollIntervalSeconds.
        _publishDelay = _meter.CreateHistogram<double>(
            "telegramlike.outbox.publish_delay",
            unit: "s",
            description: "Seconds between an event being recorded and it being published.");

        _meter.CreateObservableGauge(
            "telegramlike.outbox.pending",
            () => _backlog.PendingCount,
            unit: "{event}",
            description: "Events waiting to be published.");

        _meter.CreateObservableGauge(
            "telegramlike.outbox.dead_lettered_backlog",
            () => _backlog.DeadLetteredCount,
            unit: "{event}",
            description: "Dead-lettered events still sitting in the collection.");

        _meter.CreateObservableGauge(
            "telegramlike.outbox.oldest_pending_age",
            () => _backlog.OldestPendingAgeSeconds,
            unit: "s",
            description: "Age of the oldest unpublished event; 0 when the queue is empty.");
    }

    public void RecordPublished(string eventType, DateTime occurredAt)
    {
        var tag = new KeyValuePair<string, object?>("event_type", ShortEventType(eventType));
        _published.Add(1, tag);

        var delay = (DateTime.UtcNow - occurredAt).TotalSeconds;
        // Clock skew or a row written by a differently-skewed replica can produce a
        // negative delay; recording it would corrupt the histogram's sum.
        if (delay >= 0) _publishDelay.Record(delay, tag);
    }

    /// <summary>
    /// Counts per-event publish errors — an unresolvable event type, a bad payload, a
    /// broker rejection. It deliberately is NOT the broker-outage signal: MassTransit
    /// blocks inside Publish waiting to reconnect instead of throwing, so with RabbitMQ
    /// down this counter stays flat while the sender sits on one event (verified by
    /// stopping the broker — the queue grew to 254 with zero failures recorded).
    /// The outage signal is <c>oldest_pending_age</c>, which is what OutboxStalled alerts on.
    /// </summary>
    public void RecordPublishFailure(string eventType) =>
        _publishFailures.Add(1, new KeyValuePair<string, object?>("event_type", ShortEventType(eventType)));

    public void RecordDeadLettered(string eventType) =>
        _deadLettered.Add(1, new KeyValuePair<string, object?>("event_type", ShortEventType(eventType)));

    public void UpdateBacklog(OutboxBacklog backlog) => _backlog = backlog;

    /// <summary>
    /// The label form of a stored event type. A declared wire name ("chats.member-joined.v1")
    /// is already short, low-cardinality and stable across renames, so it is used as-is —
    /// which is exactly what makes it a better label than a class name. Legacy rows still
    /// hold an assembly-qualified CLR name and are trimmed down to "MessageSent".
    /// </summary>
    private static string ShortEventType(string eventType)
    {
        var typeName = eventType.AsSpan();

        var comma = typeName.IndexOf(',');
        if (comma < 0) return eventType;

        typeName = typeName[..comma];

        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0) typeName = typeName[(lastDot + 1)..];

        return typeName.Trim().ToString();
    }

    public void Dispose() => _meter.Dispose();
}
