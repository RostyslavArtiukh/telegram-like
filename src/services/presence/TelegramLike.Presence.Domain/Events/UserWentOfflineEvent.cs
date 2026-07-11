using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Presence.Domain.Events;

public sealed record UserWentOfflineEvent(Guid UserId, DateTime? LastSeenAt) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
