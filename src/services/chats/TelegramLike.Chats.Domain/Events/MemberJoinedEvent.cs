using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberJoinedEvent(
    Guid ChatId,
    Guid UserId,
    MemberRole Role,
    IReadOnlyList<Guid> Recipients) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
