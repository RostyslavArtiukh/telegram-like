using TelegramLike.Shared.Domain;

namespace TelegramLike.Messaging.Domain.Events;

public sealed record MessageRetractedEvent(Guid MessageId, Guid ChatId, Guid RetractedBy) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
