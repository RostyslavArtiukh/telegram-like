using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberLeftEvent(Guid ChatId, Guid UserId) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
