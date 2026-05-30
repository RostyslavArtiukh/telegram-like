using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record ChatDeletedEvent(Guid ChatId, Guid DeletedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
