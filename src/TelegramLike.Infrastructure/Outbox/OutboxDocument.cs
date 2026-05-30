using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Infrastructure.Outbox;

[BsonIgnoreExtraElements]
internal sealed class OutboxDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public DateTime? SentAt { get; set; }

    public int Retries { get; set; }

    public static OutboxDocument FromMessage(OutboxMessage message) => new()
    {
        Id = message.Id,
        EventType = message.EventType,
        Payload = message.Payload,
        OccurredAt = message.OccurredAt,
        SentAt = message.SentAt,
        Retries = message.Retries
    };

    public OutboxMessage ToMessage() =>
        new(Id, EventType, Payload, OccurredAt, SentAt, Retries);
}
