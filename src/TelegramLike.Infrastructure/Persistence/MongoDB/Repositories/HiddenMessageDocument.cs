using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;

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
