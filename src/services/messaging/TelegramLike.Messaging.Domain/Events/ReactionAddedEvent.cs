using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Domain.Events;

public sealed record ReactionAddedEvent(Guid MessageId, Guid ChatId, Guid UserId, Emoji Emoji) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
