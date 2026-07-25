using TelegramLike.Shared.Domain;

namespace TelegramLike.Identity.Domain.Events;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string Username) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
