using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberJoinedEvent(
    Guid ChatId,
    Guid UserId,
    MemberRole Role,
    IReadOnlyList<Guid> Recipients) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
