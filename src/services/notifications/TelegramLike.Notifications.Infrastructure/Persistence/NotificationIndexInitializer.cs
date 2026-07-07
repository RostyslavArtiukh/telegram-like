using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TelegramLike.Notifications.Infrastructure.Persistence;

internal sealed class NotificationIndexInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        await EnsureIndexesAsync(database, cancellationToken);
        logger.LogInformation("Notification indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Exposed so integration tests can apply the same index as production.
    // Unique partial index on (RecipientId, SourceEventId) — guards against
    // duplicate Notification rows when RabbitMQ redelivers an integration event.
    // Partial filter excludes legacy docs (created before this index existed) that
    // have no SourceEventId field, so the migration is non-breaking.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>("notifications");

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
