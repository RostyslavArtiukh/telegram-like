using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Identity.Events;

public sealed record UserBlockedEvent(Guid BlockerId, Guid BlockedUserId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
