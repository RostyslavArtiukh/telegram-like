using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Domain.Events;

public sealed record MemberRoleChangedEvent(
    Guid ChatId,
    Guid UserId,
    MemberRole OldRole,
    MemberRole NewRole,
    Guid ChangedBy) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
