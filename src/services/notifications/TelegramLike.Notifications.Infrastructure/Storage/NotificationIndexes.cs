using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Notifications.Infrastructure.Storage;

internal sealed class NotificationIndexes : IMongoIndexes
{
    public string Collection => "notifications";

    public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default) =>
        EnsureIndexesAsync(database, cancellationToken);

    // Exposed so integration tests can apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>("notifications");

        // Unique partial index on (RecipientId, SourceEventId) — guards against duplicate
        // Notification rows when RabbitMQ redelivers an integration event. The partial filter
        // excludes legacy docs (created before this index existed) that have no SourceEventId
        // field, so the migration is non-breaking.
        var uniqueSourceEvent = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("RecipientId").Ascending("SourceEventId"),
            new CreateIndexOptions<BsonDocument>
            {
                Name = "uniq_recipient_source_event",
                Unique = true,
                PartialFilterExpression = Builders<BsonDocument>.Filter.Exists("SourceEventId")
            });

        // Backs GetFeedAsync (filter RecipientId, sort CreatedAt desc). Without it Mongo
        // does an in-memory sort that fails past the 32 MB limit for a heavy recipient.
        // Id is the tiebreaker for a stable keyset cursor over near-identical CreatedAt.
        var feed = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("RecipientId").Descending("CreatedAt").Descending("_id"),
            new CreateIndexOptions { Name = "recipient_created" });

        // Backs GetUnreadCountAsync and the unreadOnly feed filter.
        var unread = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("RecipientId").Ascending("Status"),
            new CreateIndexOptions { Name = "recipient_status" });

        return collection.Indexes.CreateManyAsync([uniqueSourceEvent, feed, unread], cancellationToken);
    }
}
