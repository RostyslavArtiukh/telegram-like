using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class MessageReadReceiptDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid MessageId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid MemberId { get; set; }

    public DateTime ReadAt { get; set; }
}
