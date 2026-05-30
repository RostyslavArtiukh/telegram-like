using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record MemberKickedEvent(
    Guid ChatId,
    Guid UserId,
    Guid KickedBy,
    IReadOnlyList<Guid> Recipients) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
