using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberBannedEvent(Guid ChatId, Guid UserId, Guid BannedBy, string? Reason) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
