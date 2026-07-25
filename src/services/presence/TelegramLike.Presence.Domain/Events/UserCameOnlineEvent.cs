using TelegramLike.Shared.Domain;

namespace TelegramLike.Presence.Domain.Events;

public sealed record UserCameOnlineEvent(Guid UserId, DateTime At) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
