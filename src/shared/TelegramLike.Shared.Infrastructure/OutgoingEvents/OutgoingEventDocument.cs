using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

[BsonIgnoreExtraElements]
public sealed class OutgoingEventDocument
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

    // Lease taken by a sender replica while it attempts this row. Another replica
    // (or this one after a crash) only re-picks it once the lease has expired, so
    // scaling the sender past one instance no longer double-publishes every event.
    [BsonIgnoreIfNull]
    public DateTime? ClaimedUntil { get; set; }

    // Which claim attempt won this row. Two replicas can pick the same candidates, but only
    // one update per row passes the lease check, so reading back by token is how a replica
    // learns which of its candidates it actually got — without a round-trip per row.
    [BsonIgnoreIfNull]
    public string? ClaimToken { get; set; }

    public static OutgoingEventDocument FromEvent(OutgoingEvent outgoingEvent) => new()
    {
        Id = outgoingEvent.Id,
        EventType = outgoingEvent.EventType,
        Payload = outgoingEvent.Payload,
        OccurredAt = outgoingEvent.OccurredAt,
        SentAt = outgoingEvent.SentAt,
        Retries = outgoingEvent.Retries,
        DeadLetteredAt = outgoingEvent.DeadLetteredAt,
        LastError = outgoingEvent.LastError
    };

    public OutgoingEvent ToEvent() =>
        new(Id, EventType, Payload, OccurredAt, SentAt, Retries, DeadLetteredAt, LastError);
}
