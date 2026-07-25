using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberKickedEvent(
    Guid ChatId,
    Guid UserId,
    Guid KickedBy,
    IReadOnlyList<Guid> Recipients) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
