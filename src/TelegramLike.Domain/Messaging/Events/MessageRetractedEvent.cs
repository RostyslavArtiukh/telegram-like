using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Messaging.Events;

public sealed record MessageRetractedEvent(Guid MessageId, Guid ChatId, Guid RetractedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
