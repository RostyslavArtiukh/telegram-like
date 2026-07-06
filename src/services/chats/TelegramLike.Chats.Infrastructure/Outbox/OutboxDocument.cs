using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Chats.Infrastructure.Outbox;

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

    [BsonIgnoreIfNull]
    public DateTime? DeadLetteredAt { get; set; }

    [BsonIgnoreIfNull]
    public string? LastError { get; set; }

    // Lease taken by a publisher replica while it attempts this row. Another replica
    // (or this one after a crash) only re-picks it once the lease has expired, so
    // scaling the publisher past one instance no longer double-publishes every event.
    [BsonIgnoreIfNull]
    public DateTime? ClaimedUntil { get; set; }

    public static OutboxDocument FromMessage(OutboxMessage message) => new()
    {
        Id = message.Id,
        EventType = message.EventType,
        Payload = message.Payload,
        OccurredAt = message.OccurredAt,
        SentAt = message.SentAt,
        Retries = message.Retries,
        DeadLetteredAt = message.DeadLetteredAt,
        LastError = message.LastError
    };

    public OutboxMessage ToMessage() =>
        new(Id, EventType, Payload, OccurredAt, SentAt, Retries, DeadLetteredAt, LastError);
}
