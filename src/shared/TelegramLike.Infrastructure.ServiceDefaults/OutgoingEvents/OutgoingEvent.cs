namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

/// <summary>
/// One integration event waiting in the outgoing queue: saved to the database in the
/// same transaction as the change that produced it, then published to RabbitMQ by the
/// <see cref="OutgoingEventsSender"/> (the classic transactional-outbox pattern).
/// </summary>
public sealed record OutgoingEvent(
    Guid Id,
    string EventType,
    string Payload,
    DateTime OccurredAt,
    DateTime? SentAt = null,
    int Retries = 0,
    DateTime? DeadLetteredAt = null,
    string? LastError = null);
