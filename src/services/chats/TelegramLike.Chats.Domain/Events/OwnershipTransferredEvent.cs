using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Domain.Events;

public sealed record OwnershipTransferredEvent(Guid ChatId, Guid PreviousOwner, Guid NewOwner) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
