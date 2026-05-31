using System.Text.Json;
using FluentAssertions;
using TelegramLike.Application.Messaging.IntegrationEvents;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.Messaging.Aggregates;
using TelegramLike.Domain.Messaging.ValueObjects;
using TelegramLike.Infrastructure.Outbox;
using TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;
using TelegramLike.Infrastructure.Tests.Fixtures;

namespace TelegramLike.Infrastructure.Tests.Outbox;

[Collection(IntegrationCollection.Name)]
public class OutboxIntegrationTests(IntegrationContainersFixture fx)
{
    private MongoOutboxStore NewStore() => new(fx.Database);

    private OutboxDomainEventDispatcher NewDispatcher() =>
        new(new[] { new MessageSentEventMapper() }, NewStore());

    private MessageRepository NewMessageRepo() =>
        new(fx.MongoClient, fx.Database, NewDispatcher());

    [Fact]
    public async Task AddAsync_persists_message_and_outbox_atomically()
    {
        var repo = NewMessageRepo();
        var store = NewStore();

        var recipients = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var message = Message.Send(
            chatId: Guid.NewGuid(),
            authorId: Guid.NewGuid(),
            content: MessageContent.Create("hello outbox"),
            recipients: recipients);

        await repo.AddAsync(message);

        var pending = await store.GetPendingAsync(batchSize: 1000);
        var entry = pending.Single(m =>
            m.EventType.Contains(nameof(MessageSentIntegrationEvent)) &&
            JsonSerializer.Deserialize<MessageSentIntegrationEvent>(m.Payload)!.MessageId == message.Id);

        var payload = JsonSerializer.Deserialize<MessageSentIntegrationEvent>(entry.Payload);
        payload.Should().NotBeNull();
        payload!.MessageId.Should().Be(message.Id);
        payload.ChatId.Should().Be(message.ChatId);
        payload.AuthorId.Should().Be(message.AuthorId);
        payload.Recipients.Should().BeEquivalentTo(recipients);

        message.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkSentAsync_excludes_from_pending()
    {
        var store = NewStore();
        var msg = new OutboxMessage(
            Id: Guid.NewGuid(),
            EventType: "X",
            Payload: "{}",
            OccurredAt: DateTime.UtcNow);

        using var session = await fx.MongoClient.StartSessionAsync();
        await session.WithTransactionAsync(async (s, ct) =>
        {
            await store.AddAsync(new[] { msg }, s, ct);
            return true;
        });

        await store.MarkSentAsync(msg.Id);

        var pending = await store.GetPendingAsync(batchSize: 100);
        pending.Should().NotContain(m => m.Id == msg.Id);
    }

    [Fact]
    public async Task RecordFailureAsync_bumps_counter_and_keeps_message_pending_below_max()
    {
        var store = NewStore();
        var msg = new OutboxMessage(
            Id: Guid.NewGuid(),
            EventType: "X",
            Payload: "{}",
            OccurredAt: DateTime.UtcNow);

        using var session = await fx.MongoClient.StartSessionAsync();
        await session.WithTransactionAsync(async (s, ct) =>
        {
            await store.AddAsync(new[] { msg }, s, ct);
            return true;
        });

        await store.RecordFailureAsync(msg.Id, "broker down", maxRetries: 5);
        await store.RecordFailureAsync(msg.Id, "broker still down", maxRetries: 5);

        var pending = await store.GetPendingAsync(batchSize: 100);
        var entry = pending.Single(m => m.Id == msg.Id);
        entry.Retries.Should().Be(2);
        entry.DeadLetteredAt.Should().BeNull();
        entry.LastError.Should().Be("broker still down");
    }

    [Fact]
    public async Task RecordFailureAsync_dead_letters_message_after_reaching_max_retries()
    {
        var store = NewStore();
        var msg = new OutboxMessage(
            Id: Guid.NewGuid(),
            EventType: "poison",
            Payload: "{}",
            OccurredAt: DateTime.UtcNow);

        using var session = await fx.MongoClient.StartSessionAsync();
        await session.WithTransactionAsync(async (s, ct) =>
        {
            await store.AddAsync(new[] { msg }, s, ct);
            return true;
        });

        const int maxRetries = 3;
        for (var i = 0; i < maxRetries; i++)
            await store.RecordFailureAsync(msg.Id, $"attempt {i + 1} failed", maxRetries);

        var pending = await store.GetPendingAsync(batchSize: 100);
        pending.Should().NotContain(m => m.Id == msg.Id,
            "a dead-lettered message must never be picked up again — that is the whole point");

        var dlq = await store.GetDeadLetteredAsync(batchSize: 100);
        var entry = dlq.Single(m => m.Id == msg.Id);
        entry.Retries.Should().Be(maxRetries);
        entry.DeadLetteredAt.Should().NotBeNull();
        entry.LastError.Should().Be("attempt 3 failed");
    }
}
