using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Common;

namespace TelegramLike.Domain.Chats.Events;

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
