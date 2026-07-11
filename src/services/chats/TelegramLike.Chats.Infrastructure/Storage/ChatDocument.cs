using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Infrastructure.Storage;

internal sealed class ChatDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ChatType Type { get; set; }

    public string? Name { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}
