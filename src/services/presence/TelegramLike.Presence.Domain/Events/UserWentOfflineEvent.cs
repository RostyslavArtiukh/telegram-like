using TelegramLike.Presence.Domain.Common;

namespace TelegramLike.Presence.Domain.Events;

public sealed record UserWentOfflineEvent(Guid UserId, DateTime? LastSeenAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
