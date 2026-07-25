using TelegramLike.Shared.Domain;

namespace TelegramLike.Messaging.Domain.Events;

public sealed record MessageSentEvent(
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    Guid? ReplyToMessageId,
    Guid? ForwardOriginalMessageId,
    IReadOnlyList<Guid> Recipients) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
