using TelegramLike.Notifications.Domain.Common;
using TelegramLike.Notifications.Domain.Events;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Domain.Aggregates;

public sealed class Notification : AggregateRoot
{
    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationPayload Payload { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification() { }

    private Notification(
        Guid id,
        Guid recipientId,
        NotificationType type,
        NotificationPayload payload,
        NotificationStatus status,
        DateTime createdAt,
        DateTime? readAt)
        : base(id)
    {
        RecipientId = recipientId;
        Type = type;
        Payload = payload;
        Status = status;
        CreatedAt = createdAt;
        ReadAt = readAt;
    }

    public static Notification Create(Guid recipientId, NotificationType type, NotificationPayload payload)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("RecipientId cannot be empty.", nameof(recipientId));

        var notification = new Notification(
            Guid.NewGuid(),
            recipientId,
            type,
            payload,
            NotificationStatus.Pending,
            DateTime.UtcNow,
            readAt: null);

        notification.RaiseDomainEvent(new NotificationCreatedEvent(
            notification.Id, recipientId, type, payload));

        return notification;
    }

    public static Notification Reconstitute(
        Guid id,
        Guid recipientId,
        NotificationType type,
        NotificationPayload payload,
        NotificationStatus status,
        DateTime createdAt,
        DateTime? readAt)
        => new(id, recipientId, type, payload, status, createdAt, readAt);

    public void MarkAsDelivered()
    {
        if (Status != NotificationStatus.Pending) return;
        Status = NotificationStatus.Delivered;
    }

    public void MarkAsRead()
    {
        if (Status == NotificationStatus.Read) return;

        Status = NotificationStatus.Read;
        ReadAt = DateTime.UtcNow;
        RaiseDomainEvent(new NotificationReadEvent(Id, RecipientId, ReadAt.Value));
    }
}
