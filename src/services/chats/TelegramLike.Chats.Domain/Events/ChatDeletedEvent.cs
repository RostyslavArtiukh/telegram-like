using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Domain.Events;

public sealed record ChatDeletedEvent(Guid ChatId, Guid DeletedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
