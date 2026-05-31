using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Persistence;

internal sealed class NotificationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid RecipientId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public NotificationType Type { get; set; }

    public NotificationPayloadDocument Payload { get; set; } = null!;

    [BsonRepresentation(BsonType.String)]
    public NotificationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfDefault]
    public Guid SourceEventId { get; set; }

    public static NotificationDocument FromDomain(Notification notification) => new()
    {
        Id = notification.Id,
        RecipientId = notification.RecipientId,
        Type = notification.Type,
        Payload = new NotificationPayloadDocument
        {
            ChatId = notification.Payload.ChatId,
            MessageId = notification.Payload.MessageId,
            ActorId = notification.Payload.ActorId
        },
        Status = notification.Status,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt,
        SourceEventId = notification.SourceEventId
    };

    public Notification ToDomain() => Notification.Reconstitute(
        Id,
        RecipientId,
        Type,
        NotificationPayload.Reconstitute(Payload.ChatId, Payload.MessageId, Payload.ActorId),
        Status,
        CreatedAt,
        ReadAt,
        SourceEventId);
}

internal sealed class NotificationPayloadDocument
{
    [BsonRepresentation(BsonType.String)]
    public Guid ChatId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public Guid? MessageId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public Guid? ActorId { get; set; }
}
