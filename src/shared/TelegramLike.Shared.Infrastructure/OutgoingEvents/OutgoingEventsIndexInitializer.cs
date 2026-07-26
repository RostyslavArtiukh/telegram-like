using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Indexes the outgoing-events collection. Both hot paths — the sender claiming the next
/// batch and the backlog poller counting/aging the queue — filter on
/// (SentAt, DeadLetteredAt) then order by OccurredAt, so without this they degrade into
/// full scans as history grows. A TTL index bounds that history.
/// </summary>
internal sealed class OutgoingEventsIndexInitializer(
    IServiceScopeFactory scopeFactory,
    IOptions<OutgoingEventsSenderOptions> options,
    ILogger<OutgoingEventsIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var retentionDays = options.Value.SentRetentionDays;

        await EnsureIndexesAsync(database, TimeSpan.FromDays(retentionDays), cancellationToken);

        logger.LogInformation(
            "Outgoing-events indexes ensured. Published rows expire {RetentionDays} day(s) after being sent.",
            retentionDays);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private const string CollectionName = "outgoing_events";
    private const string PendingIndexName = "pending_by_age";
    private const string SentTtlIndexName = "sent_ttl";

    // Exposed so integration tests apply the same indexes as production.
    public static async Task EnsureIndexesAsync(
        IMongoDatabase database,
        TimeSpan sentRetention,
        CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var pendingIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("SentAt")
                .Ascending("DeadLetteredAt")
                .Ascending("OccurredAt"),
            new CreateIndexOptions { Name = PendingIndexName });

        // The TTL keys off SentAt rather than OccurredAt on purpose: Mongo only expires a
        // document when the indexed field actually holds a date, and a row carries
        // SentAt = null until it reaches the broker. So pending rows — and dead-lettered
        // ones, which never get a SentAt — are immune to this sweep no matter how old they
        // are. Only history that has already been published expires.
        var sentTtlIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("SentAt"),
            new CreateIndexOptions { Name = SentTtlIndexName, ExpireAfter = sentRetention });

        await collection.Indexes.CreateOneAsync(pendingIndex, cancellationToken: cancellationToken);

        try
        {
            await collection.Indexes.CreateOneAsync(sentTtlIndex, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Code == 85 || ex.CodeName == "IndexOptionsConflict")
        {
            // The index exists with a different expireAfterSeconds, and re-creating a TTL
            // index with new options is an error rather than an update. collMod is the only
            // way to change it in place — without this branch, editing
            // OutgoingEvents:SentRetentionDays would be silently ignored on every
            // environment that already ran once.
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument
                {
                    { "collMod", CollectionName },
                    {
                        "index", new BsonDocument
                        {
                            { "name", SentTtlIndexName },
                            { "expireAfterSeconds", (int)sentRetention.TotalSeconds }
                        }
                    }
                },
                cancellationToken: cancellationToken);
        }
    }
}
