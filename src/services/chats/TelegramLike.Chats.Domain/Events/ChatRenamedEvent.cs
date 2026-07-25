using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Domain.Events;

public sealed record ChatRenamedEvent(Guid ChatId, string OldName, string NewName, Guid RenamedBy) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
