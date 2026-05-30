using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record MemberJoinedEvent(
    Guid ChatId,
    Guid UserId,
    MemberRole Role,
    IReadOnlyList<Guid> Recipients) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
