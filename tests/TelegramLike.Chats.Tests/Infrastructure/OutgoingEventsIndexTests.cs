using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

namespace TelegramLike.Chats.Tests.Infrastructure;

/// <summary>
/// The TTL index is the only thing standing between outgoing_events and unbounded growth —
/// the sender marks rows sent instead of deleting them. Like the backlog tests, this lives
/// here because the Chats project already owns the Mongo fixture.
/// </summary>
[Collection(MongoCollection.Name)]
public class OutgoingEventsIndexTests(MongoFixture fx)
{
    private static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(7);

    // A private database per test: indexes are per-collection state, and a neighbouring
    // test changing the retention would flip the expected expireAfterSeconds.
    private async Task<IMongoDatabase> NewIndexedDatabaseAsync(TimeSpan? retention = null)
    {
        var database = fx.MongoClient.GetDatabase($"tl_outbox_idx_test_{Guid.NewGuid():N}");
        await OutgoingEventsIndexInitializer.EnsureIndexesAsync(database, retention ?? DefaultRetention);
        return database;
    }

    private static async Task<BsonDocument?> FindIndexAsync(IMongoDatabase database, string name)
    {
        var indexes = await database
            .GetCollection<BsonDocument>("outgoing_events")
            .Indexes.ListAsync();

        var all = await indexes.ToListAsync();
        return all.FirstOrDefault(index => index["name"].AsString == name);
    }

    [Fact]
    public async Task EnsureIndexes_ExpiresSentRowsAfterTheConfiguredRetention()
    {
        var database = await NewIndexedDatabaseAsync();

        var ttl = await FindIndexAsync(database, "sent_ttl");

        ttl.Should().NotBeNull();
        // Keyed on SentAt, not OccurredAt: Mongo expires a document only when the indexed
        // field holds a date, and a row keeps SentAt null until it reaches the broker. That
        // is what makes pending and dead-lettered rows immune to the sweep.
        ttl!["key"].AsBsonDocument.Names.Should().Equal("SentAt");
        ttl["expireAfterSeconds"].ToInt32().Should().Be((int)DefaultRetention.TotalSeconds);
    }

    [Fact]
    public async Task EnsureIndexes_KeepsThePendingQueryIndex()
    {
        var database = await NewIndexedDatabaseAsync();

        var pending = await FindIndexAsync(database, "pending_by_age");

        pending.Should().NotBeNull();
        pending!["key"].AsBsonDocument.Names.Should().Equal("SentAt", "DeadLetteredAt", "OccurredAt");
    }

    [Fact]
    public async Task EnsureIndexes_WhenRetentionChanges_UpdatesTheExistingTtlIndex()
    {
        var database = await NewIndexedDatabaseAsync();

        await OutgoingEventsIndexInitializer.EnsureIndexesAsync(database, TimeSpan.FromDays(2));

        // Re-creating a TTL index with different options is an error in Mongo, not an
        // update — so without the collMod fallback a retention change would be silently
        // dropped on every environment that had already started once.
        var ttl = await FindIndexAsync(database, "sent_ttl");
        ttl!["expireAfterSeconds"].ToInt32().Should().Be((int)TimeSpan.FromDays(2).TotalSeconds);
    }

    [Fact]
    public async Task EnsureIndexes_WhenRunTwiceWithTheSameRetention_DoesNotThrow()
    {
        var database = await NewIndexedDatabaseAsync();

        // Every service restart re-runs this hosted service.
        var rerun = async () =>
            await OutgoingEventsIndexInitializer.EnsureIndexesAsync(database, DefaultRetention);

        await rerun.Should().NotThrowAsync();
    }
}
