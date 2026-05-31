using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Presence.Infrastructure.Persistence;

[BsonIgnoreExtraElements]
internal sealed class ChatMembershipDocument
{
    // {chatId}:{userId} — composite key gives natural per-pair uniqueness
    // and idempotent upserts without a separate index.
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid ChatId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    public static string MakeId(Guid chatId, Guid userId) => $"{chatId:N}:{userId:N}";
}
