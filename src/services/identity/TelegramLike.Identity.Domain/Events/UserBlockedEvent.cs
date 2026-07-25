using TelegramLike.Shared.Domain;

namespace TelegramLike.Identity.Domain.Events;

public sealed record UserBlockedEvent(Guid BlockerId, Guid BlockedUserId) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
