using TelegramLike.Shared.Domain;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Domain.Events;

public sealed record ReactionRemovedEvent(Guid MessageId, Guid ChatId, Guid UserId, Emoji Emoji) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
