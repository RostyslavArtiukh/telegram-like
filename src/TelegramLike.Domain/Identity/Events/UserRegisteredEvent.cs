using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Identity.Events;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string Username) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
