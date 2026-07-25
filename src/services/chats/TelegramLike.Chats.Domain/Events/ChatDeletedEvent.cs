using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Domain.Events;

public sealed record ChatDeletedEvent(Guid ChatId, Guid DeletedBy) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
