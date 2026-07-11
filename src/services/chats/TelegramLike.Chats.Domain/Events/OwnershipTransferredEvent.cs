using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Chats.Domain.Events;

public sealed record OwnershipTransferredEvent(Guid ChatId, Guid PreviousOwner, Guid NewOwner) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
