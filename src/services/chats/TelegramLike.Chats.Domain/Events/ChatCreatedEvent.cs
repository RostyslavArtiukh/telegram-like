using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Domain.Events;

public sealed record ChatCreatedEvent(Guid ChatId, ChatType Type, Guid CreatedBy) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
