using FluentAssertions;
using MongoDB.Driver;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;
using TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

namespace TelegramLike.Chats.Tests.Infrastructure;

/// <summary>
/// GetBacklogAsync feeds the outbox gauges and the OutboxStalled alert, so what it counts
/// as "backlog" is the definition operators end up trusting. It lives in the shared outbox
/// but is exercised here because this project already has the replica-set Mongo fixture.
/// </summary>
[Collection(MongoCollection.Name)]
public class OutgoingEventsStoreBacklogTests(MongoFixture fx)
{
    // A private database per test: the backlog is a whole-collection aggregate, so rows
    // left by a neighbouring test would silently change the expected counts.
    private OutgoingEventsStore NewStore(out IMongoCollection<OutgoingEventDocument> collection)
    {
        var database = fx.MongoClient.GetDatabase($"tl_outbox_test_{Guid.NewGuid():N}");
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
        EventType = "TelegramLike.Contracts.SomethingHappened, TelegramLike.Contracts",
        Payload = "{}",
        OccurredAt = occurredAt,
        SentAt = sentAt,
        DeadLetteredAt = deadLetteredAt,
        ClaimedUntil = claimedUntil
    };

    [Fact]
    public async Task GetBacklog_WhenQueueIsEmpty_ReportsZeros()
    {
        var store = NewStore(out _);

        var backlog = await store.GetBacklogAsync();

        backlog.PendingCount.Should().Be(0);
        backlog.DeadLetteredCount.Should().Be(0);
        // Zero rather than null: the gauge has to stay a continuous line in Grafana.
        backlog.OldestPendingAgeSeconds.Should().Be(0);
    }

    [Fact]
    public async Task GetBacklog_CountsOnlyUnsentUndeadLetteredRows()
    {
        var store = NewStore(out var collection);
        var now = DateTime.UtcNow;

        await collection.InsertManyAsync([
            Row(now.AddSeconds(-30)),
            Row(now.AddSeconds(-10)),
            Row(now.AddMinutes(-5), sentAt: now.AddMinutes(-4)),
            Row(now.AddMinutes(-9), deadLetteredAt: now.AddMinutes(-8))
        ]);

        var backlog = await store.GetBacklogAsync();

        backlog.PendingCount.Should().Be(2);
        backlog.DeadLetteredCount.Should().Be(1);
    }

    [Fact]
    public async Task GetBacklog_AgesFromTheOldestPendingRow()
    {
        var store = NewStore(out var collection);
        var now = DateTime.UtcNow;

        await collection.InsertManyAsync([
            Row(now.AddSeconds(-5)),
            Row(now.AddSeconds(-120)),
            Row(now.AddSeconds(-40))
        ]);

        var backlog = await store.GetBacklogAsync();

        // The head of the queue is what "lag" means — not the newest arrival.
        backlog.OldestPendingAgeSeconds.Should().BeApproximately(120, precision: 5);
    }

    [Fact]
    public async Task GetBacklog_IgnoresTheOldestRowOnceItIsSent()
    {
        var store = NewStore(out var collection);
        var now = DateTime.UtcNow;

        await collection.InsertManyAsync([
            Row(now.AddSeconds(-300), sentAt: now),
            Row(now.AddSeconds(-20))
        ]);

        var backlog = await store.GetBacklogAsync();

        backlog.OldestPendingAgeSeconds.Should().BeApproximately(20, precision: 5);
    }

    [Fact]
    public async Task GetBacklog_StillCountsRowsAnotherSenderHasClaimed()
    {
        var store = NewStore(out var collection);
        var now = DateTime.UtcNow;

        // GetPendingAsync deliberately skips leased rows so replicas get disjoint
        // batches. The backlog must NOT: a row being published is still unpublished,
        // and hiding it would make a sender that dies mid-publish look healthy.
        await collection.InsertManyAsync([
            Row(now.AddSeconds(-45), claimedUntil: now.AddSeconds(15))
        ]);

        var backlog = await store.GetBacklogAsync();

        backlog.PendingCount.Should().Be(1);
        backlog.OldestPendingAgeSeconds.Should().BeApproximately(45, precision: 5);
    }
}
