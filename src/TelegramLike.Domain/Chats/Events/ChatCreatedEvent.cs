using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record ChatCreatedEvent(Guid ChatId, ChatType Type, Guid CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
