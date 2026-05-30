using TelegramLike.Notifications.Domain.Common;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Domain.Events;

public sealed record NotificationCreatedEvent(
    Guid NotificationId,
    Guid RecipientId,
    NotificationType Type,
    NotificationPayload Payload) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
