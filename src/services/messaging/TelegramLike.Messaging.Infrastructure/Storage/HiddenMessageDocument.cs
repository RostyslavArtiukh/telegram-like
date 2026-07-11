using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Messaging.Infrastructure.Storage;

internal sealed class HiddenMessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid MessageId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }
}
