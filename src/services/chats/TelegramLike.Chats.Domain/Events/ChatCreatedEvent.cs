using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Domain.Events;

public sealed record ChatCreatedEvent(Guid ChatId, ChatType Type, Guid CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
