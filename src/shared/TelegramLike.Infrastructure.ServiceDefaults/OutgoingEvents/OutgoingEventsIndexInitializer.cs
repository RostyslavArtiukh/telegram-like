using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

/// <summary>
/// Indexes the outgoing-events collection. Both hot paths — the sender claiming the next
/// batch and the backlog poller counting/aging the queue — filter on
/// (SentAt, DeadLetteredAt) then order by OccurredAt, and the collection keeps every
/// already-sent row, so without this they degrade into full scans as history grows.
/// </summary>
internal sealed class OutgoingEventsIndexInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<OutgoingEventsIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        await EnsureIndexesAsync(database, cancellationToken);
        logger.LogInformation("Outgoing-events indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Exposed so integration tests apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>("outgoing_events");
        var pendingIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("SentAt")
                .Ascending("DeadLetteredAt")
                .Ascending("OccurredAt"),
            new CreateIndexOptions { Name = "pending_by_age" });

        return collection.Indexes.CreateOneAsync(pendingIndex, cancellationToken: cancellationToken);
    }
}
