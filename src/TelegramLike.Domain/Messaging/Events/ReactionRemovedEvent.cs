using TelegramLike.Domain.Common;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Domain.Messaging.Events;

public sealed record ReactionRemovedEvent(Guid MessageId, Guid ChatId, Guid UserId, Emoji Emoji) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
