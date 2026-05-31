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
        logger.LogInformation("Notification unique index ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Exposed so integration tests can apply the same index as production.
    // Unique partial index on (RecipientId, SourceEventId) — guards against
    // duplicate Notification rows when RabbitMQ redelivers an integration event.
    // Partial filter excludes legacy docs (created before this index existed) that
    // have no SourceEventId field, so the migration is non-breaking.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<BsonDocument>("notifications");

        var keys = Builders<BsonDocument>.IndexKeys
            .Ascending("RecipientId")
            .Ascending("SourceEventId");

        var options = new CreateIndexOptions<BsonDocument>
        {
            Name = "uniq_recipient_source_event",
            Unique = true,
            PartialFilterExpression = Builders<BsonDocument>.Filter.Exists("SourceEventId")
        };

        return collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys, options),
            cancellationToken: ct);
    }
}
