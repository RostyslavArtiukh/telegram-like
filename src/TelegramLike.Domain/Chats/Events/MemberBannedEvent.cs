using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

public sealed record MemberBannedEvent(Guid ChatId, Guid UserId, Guid BannedBy, string? Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
