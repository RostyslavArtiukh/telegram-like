using System.Text.Json;
using FluentAssertions;
using MongoDB.Driver;
using TelegramLike.Chats.Application.IntegrationEvents;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;
using TelegramLike.Contracts.Chats;
using TelegramLike.Shared.Application;
using TelegramLike.Shared.Domain;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

namespace TelegramLike.Chats.Tests.Infrastructure;

/// <summary>
/// The writer is the seam every published event passes through — map, serialize, queue —
/// yet every repository test substitutes a double for it, so nothing exercised the real one.
/// It lives in the shared outbox but is covered here because this project already has the
/// replica-set Mongo fixture (transactions are part of its contract).
/// </summary>
[Collection(MongoCollection.Name)]
public class OutgoingEventsWriterTests(MongoFixture fx)
{
    private OutgoingEventsWriter NewWriter(
        out IMongoCollection<OutgoingEventDocument> collection,
        IntegrationEventMap? map = null)
    {
        var database = fx.MongoClient.GetDatabase($"tl_outbox_writer_test_{Guid.NewGuid():N}");
        collection = database.GetCollection<OutgoingEventDocument>("outgoing_events");
        return new OutgoingEventsWriter(map ?? ChatsIntegrationEvents.Map, new OutgoingEventsStore(database));
    }

    private async Task WriteAsync(OutgoingEventsWriter writer, params IChangeEvent[] events)
    {
        using var session = await fx.MongoClient.StartSessionAsync();
        await session.WithTransactionAsync(async (s, token) =>
        {
            await writer.WriteAsync(events, s, token);
            return true;
        });
    }

    [Fact]
    public async Task Write_QueuesTheMappedIntegrationEvent()
    {
        var writer = NewWriter(out var collection);
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await WriteAsync(writer, new MemberBannedEvent(chatId, userId, Guid.NewGuid(), "spam"));

        var row = await collection.Find(FilterDefinition<OutgoingEventDocument>.Empty).SingleAsync();
        var payload = JsonSerializer.Deserialize<MemberBannedIntegrationEvent>(row.Payload)!;
        payload.ChatId.Should().Be(chatId);
        payload.UserId.Should().Be(userId);
        payload.Reason.Should().Be("spam");
    }

    [Fact]
    public async Task Write_StoresTheDeclaredWireNameTheSenderCanResolve()
    {
        // A CLR name ties every queued row to the class keeping its name and namespace, so a
        // rename strands rows a rollback can't rescue — they carry the old build's name.
        var writer = NewWriter(out var collection);

        await WriteAsync(writer, new MemberLeftEvent(Guid.NewGuid(), Guid.NewGuid()));

        var row = await collection.Find(FilterDefinition<OutgoingEventDocument>.Empty).SingleAsync();
        row.EventType.Should().Be("chats.member-left.v1");
        IntegrationEventNames.Resolve(row.EventType).Should().Be(typeof(MemberLeftIntegrationEvent));
    }

    [Fact]
    public async Task Write_KeepsTheChangeEventsOccurredAt_NotTheWriteTime()
    {
        // The sender orders the queue by OccurredAt and the backlog gauge measures age from
        // it; stamping "now" here would make both meaningless.
        var writer = NewWriter(out var collection);
        var source = new MemberLeftEvent(Guid.NewGuid(), Guid.NewGuid());

        await WriteAsync(writer, source);

        var row = await collection.Find(FilterDefinition<OutgoingEventDocument>.Empty).SingleAsync();
        row.OccurredAt.Should().BeCloseTo(source.OccurredAt, TimeSpan.FromMilliseconds(1));
        row.SentAt.Should().BeNull("a freshly queued row is pending, not sent");
    }

    [Fact]
    public async Task Write_SkipsEventsTheServiceKeepsInternal()
    {
        var writer = NewWriter(out var collection);

        await WriteAsync(writer, new ChatRenamedEvent(Guid.NewGuid(), "old", "new", Guid.NewGuid()));

        (await collection.CountDocumentsAsync(FilterDefinition<OutgoingEventDocument>.Empty))
            .Should().Be(0, "an event with no arm in the map must not reach the wire");
    }

    [Fact]
    public async Task Write_QueuesOnlyThePublishableEventsOfAMixedBatch()
    {
        // What TransferOwnership actually drains: two role changes plus an internal-only
        // ownership record.
        var writer = NewWriter(out var collection);
        var chatId = Guid.NewGuid();

        await WriteAsync(writer,
            new MemberRoleChangedEvent(chatId, Guid.NewGuid(), MemberRole.Owner, MemberRole.Admin, Guid.NewGuid()),
            new MemberRoleChangedEvent(chatId, Guid.NewGuid(), MemberRole.Member, MemberRole.Owner, Guid.NewGuid()),
            new OwnershipTransferredEvent(chatId, Guid.NewGuid(), Guid.NewGuid()));

        var rows = await collection.Find(FilterDefinition<OutgoingEventDocument>.Empty).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.EventType == "chats.member-role-changed.v1");
    }

    [Fact]
    public async Task Write_WithNoEvents_DoesNotThrow()
    {
        var writer = NewWriter(out _);

        var act = async () => await WriteAsync(writer);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Write_RollsBackWithTheEnclosingTransaction()
    {
        // The whole point of the outbox: the queued row and the aggregate write live or die
        // together. If the caller's transaction aborts, no event may be left behind.
        var writer = NewWriter(out var collection);

        using var session = await fx.MongoClient.StartSessionAsync();
        var act = async () => await session.WithTransactionAsync<bool>(async (s, token) =>
        {
            await writer.WriteAsync([new MemberLeftEvent(Guid.NewGuid(), Guid.NewGuid())], s, token);
            throw new InvalidOperationException("aggregate write failed");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await collection.CountDocumentsAsync(FilterDefinition<OutgoingEventDocument>.Empty)).Should().Be(0);
    }
}
