using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Infrastructure.Persistence;

internal sealed class UserPresenceDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public OnlineStatus Status { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public bool HideLastSeen { get; set; }

    public static UserPresenceDocument FromDomain(UserPresence presence) => new()
    {
        Id = presence.Id,
        Status = presence.Status,
        LastSeenAt = presence.LastSeenAt,
        HideLastSeen = presence.HideLastSeen
    };

    public UserPresence ToDomain() =>
        UserPresence.Reconstitute(Id, Status, LastSeenAt, HideLastSeen);
}
