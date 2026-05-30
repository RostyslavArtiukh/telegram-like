using TelegramLike.Presence.Domain.Common;

namespace TelegramLike.Presence.Domain.Events;

public sealed record UserCameOnlineEvent(Guid UserId, DateTime At) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
