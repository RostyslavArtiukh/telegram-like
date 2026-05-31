namespace TelegramLike.Infrastructure.Outbox;

internal sealed record OutboxMessage(
    Guid Id,
    string EventType,
    string Payload,
    DateTime OccurredAt,
    DateTime? SentAt = null,
    int Retries = 0,
    DateTime? DeadLetteredAt = null,
    string? LastError = null);
