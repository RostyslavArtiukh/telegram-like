using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberRoleChangedEvent(
    Guid ChatId,
    Guid UserId,
    MemberRole OldRole,
    MemberRole NewRole,
    Guid ChangedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
