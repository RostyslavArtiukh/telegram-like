using FluentAssertions;
using MongoDB.Driver;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

namespace TelegramLike.Chats.Tests.Infrastructure;

/// <summary>
/// Claiming is what lets more than one sender replica drain the same queue without publishing
/// everything twice. [TL-125] replaced a round-trip per row with three round-trips per batch,
/// and the property that had to survive that is the important one: a row is handed to exactly
/// one replica.
/// </summary>
[Collection(MongoCollection.Name)]
public class OutgoingEventsStoreClaimTests(MongoFixture fx)
{
    private OutgoingEventsStore NewStore(out IMongoCollection<OutgoingEventDocument> collection)
    {
        var database = fx.MongoClient.GetDatabase($"tl_outbox_claim_{Guid.NewGuid():N}");
        collection = database.GetCollection<OutgoingEventDocument>("outgoing_events");
        return new OutgoingEventsStore(database);
    }

    private static OutgoingEventDocument Row(
        DateTime occurredAt,
        DateTime? sentAt = null,
        DateTime? deadLetteredAt = null,
        DateTime? claimedUntil = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = "chats.member-left.v1",
        Payload = "{}",
        OccurredAt = occurredAt,
        SentAt = sentAt,
        DeadLetteredAt = deadLetteredAt,
        ClaimedUntil = claimedUntil
    };

    private static async Task SeedAsync(IMongoCollection<OutgoingEventDocument> collection, params OutgoingEventDocument[] rows)
        => await collection.InsertManyAsync(rows);

    [Fact]
    public async Task GetPending_ReturnsOldestFirst_UpToTheBatchSize()
    {
        var store = NewStore(out var collection);
        var t0 = DateTime.UtcNow.AddMinutes(-10);
        var oldest = Row(t0);
        var middle = Row(t0.AddMinutes(1));
        var newest = Row(t0.AddMinutes(2));
        await SeedAsync(collection, newest, oldest, middle);

        var claimed = await store.GetPendingAsync(2);

        claimed.Select(e => e.Id).Should().Equal(oldest.Id, middle.Id);
    }

    [Fact]
    public async Task GetPending_SkipsRowsThatAreSent_DeadLettered_OrStillLeasedByAnotherReplica()
    {
        var store = NewStore(out var collection);
        var now = DateTime.UtcNow;
        var pending = Row(now.AddMinutes(-5));
        await SeedAsync(
            collection,
            pending,
            Row(now.AddMinutes(-9), sentAt: now),
            Row(now.AddMinutes(-8), deadLetteredAt: now),
            Row(now.AddMinutes(-7), claimedUntil: now.AddMinutes(1)));

        var claimed = await store.GetPendingAsync(10);

        claimed.Select(e => e.Id).Should().Equal(pending.Id);
    }

    [Fact]
    public async Task GetPending_ReclaimsARowWhoseLeaseExpired()
    {
        // A replica that crashed mid-publish leaves its lease behind; the row has to come
        // back, or the event is stuck until someone notices.
        var store = NewStore(out var collection);
        var abandoned = Row(DateTime.UtcNow.AddMinutes(-5), claimedUntil: DateTime.UtcNow.AddSeconds(-1));
        await SeedAsync(collection, abandoned);

        var claimed = await store.GetPendingAsync(10);

        claimed.Select(e => e.Id).Should().Equal(abandoned.Id);
    }

    [Fact]
    public async Task GetPending_HandsEachRowToExactlyOneCaller_WhenSendersRaceForTheSameBatch()
    {
        // The multi-replica property. Both callers pick the same candidates; the per-document
        // lease check decides who gets what, and the read-back tells each what it actually won.
        var store = NewStore(out var collection);
        var t0 = DateTime.UtcNow.AddMinutes(-10);
        var rows = Enumerable.Range(0, 40).Select(i => Row(t0.AddSeconds(i))).ToArray();
        await SeedAsync(collection, rows);

        var races = await Task.WhenAll(
            Task.Run(() => store.GetPendingAsync(40)),
            Task.Run(() => store.GetPendingAsync(40)),
            Task.Run(() => store.GetPendingAsync(40)));

        var claimedIds = races.SelectMany(r => r.Select(e => e.Id)).ToList();
        claimedIds.Should().OnlyHaveUniqueItems("a claimed row must not be published twice");
        claimedIds.Should().BeEquivalentTo(rows.Select(r => r.Id), "and nothing may be left behind either");
    }

    [Fact]
    public async Task MarkSent_ClearsTheLease_SoASentRowIsPlainHistory()
    {
        var store = NewStore(out var collection);
        var row = Row(DateTime.UtcNow.AddMinutes(-1));
        await SeedAsync(collection, row);
        await store.GetPendingAsync(10);

        await store.MarkSentAsync(row.Id);

        var stored = await collection.Find(d => d.Id == row.Id).SingleAsync();
        stored.SentAt.Should().NotBeNull();
        stored.ClaimedUntil.Should().BeNull();
        stored.ClaimToken.Should().BeNull();
    }
}
