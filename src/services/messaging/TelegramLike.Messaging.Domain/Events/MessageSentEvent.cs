using TelegramLike.Messaging.Domain.Common;

namespace TelegramLike.Messaging.Domain.Events;

public sealed record MessageSentEvent(
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    Guid? ReplyToMessageId,
    Guid? ForwardOriginalMessageId,
    IReadOnlyList<Guid> Recipients) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
