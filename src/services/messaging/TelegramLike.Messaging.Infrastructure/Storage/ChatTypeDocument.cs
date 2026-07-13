using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Messaging.Infrastructure.Storage;

[BsonIgnoreExtraElements]
internal sealed class ChatTypeDocument
{
    // Chat id is the natural key — one doc per chat, idempotent set-once upsert.
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    // ChatType name ("Direct"/"Group"/"Broadcast").
    public string Type { get; set; } = string.Empty;
}
