using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record ChatRenamedEvent(Guid ChatId, string OldName, string NewName, Guid RenamedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
