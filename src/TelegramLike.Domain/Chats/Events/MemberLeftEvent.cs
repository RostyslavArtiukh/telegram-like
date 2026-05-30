using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record MemberLeftEvent(Guid ChatId, Guid UserId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
