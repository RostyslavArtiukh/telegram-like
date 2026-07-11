using TelegramLike.Domain.ServiceDefaults;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Domain.Events;

public sealed record ReactionAddedEvent(Guid MessageId, Guid ChatId, Guid UserId, Emoji Emoji) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
