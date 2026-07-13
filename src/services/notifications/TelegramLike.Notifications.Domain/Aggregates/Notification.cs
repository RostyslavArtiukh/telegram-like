using TelegramLike.Domain.ServiceDefaults;
using TelegramLike.Notifications.Domain.Events;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Domain.Aggregates;

public sealed class Notification : ObjectWithEvents
{
    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationPayload Payload { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public Guid SourceEventId { get; private set; }

    private Notification() { }

    private Notification(
        Guid id,
        Guid recipientId,
        NotificationType type,
        NotificationPayload payload,
        NotificationStatus status,
        DateTime createdAt,
        DateTime? readAt,
        Guid sourceEventId)
        : base(id)
    {
        RecipientId = recipientId;
        Type = type;
        Payload = payload;
        Status = status;
        CreatedAt = createdAt;
        ReadAt = readAt;
        SourceEventId = sourceEventId;
    }

    public static Notification Create(
        Guid recipientId,
        NotificationType type,
        NotificationPayload payload,
        Guid sourceEventId)
    {
        if (recipientId == Guid.Empty)
            throw new DomainException("RecipientId cannot be empty.");
        if (sourceEventId == Guid.Empty)
            throw new DomainException("SourceEventId cannot be empty.");

        var notification = new Notification(
            Guid.NewGuid(),
            recipientId,
            type,
            payload,
            NotificationStatus.Pending,
            DateTime.UtcNow,
            readAt: null,
            sourceEventId);

        notification.RecordEvent(new NotificationCreatedEvent(
            notification.Id, recipientId, type, payload));

        return notification;
    }

    public static Notification FromStorage(
        Guid id,
        Guid recipientId,
        NotificationType type,
        NotificationPayload payload,
        NotificationStatus status,
        DateTime createdAt,
        DateTime? readAt,
        Guid sourceEventId)
        => new(id, recipientId, type, payload, status, createdAt, readAt, sourceEventId);

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
        RecordEvent(new NotificationReadEvent(Id, RecipientId, ReadAt.Value));
    }
}
