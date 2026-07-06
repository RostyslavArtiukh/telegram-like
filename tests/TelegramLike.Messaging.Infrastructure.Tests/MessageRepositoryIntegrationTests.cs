using FluentAssertions;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Domain.ValueObjects;
using TelegramLike.Messaging.Infrastructure.Persistence;
using TelegramLike.Messaging.Infrastructure.Tests.Fixtures;

namespace TelegramLike.Messaging.Infrastructure.Tests;

[Collection(MongoCollection.Name)]
public class MessageRepositoryIntegrationTests(MongoFixture fx)
{
    private MessageRepository NewRepository() => new(fx.MongoClient, fx.Database, new NoOpDomainEventDispatcher());

    private static Message NewMessage(bool isBroadcast = false)
        => Message.Send(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MessageContent.Create("hi"), [Guid.NewGuid()], isBroadcast: isBroadcast);

    [Fact]
    public async Task Add_then_GetById_round_trips_the_message()
    {
        var repo = NewRepository();
        var message = NewMessage();

        await repo.AddAsync(message);
        var loaded = await repo.GetByIdAsync(message.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(message.Id);
        loaded.ChatId.Should().Be(message.ChatId);
        loaded.AuthorId.Should().Be(message.AuthorId);
        loaded.Content.Text.Should().Be("hi");
    }

    [Fact]
    public async Task GetById_for_unknown_id_returns_null()
    {
        var repo = NewRepository();

        var loaded = await repo.GetByIdAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Add_with_duplicate_message_id_is_swallowed_idempotently()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var first = Message.Send(messageId, chatId, authorId, MessageContent.Create("first"), [Guid.NewGuid()]);
        await repo.AddAsync(first);

        // A retried send reuses the same client-supplied id.
        var retry = Message.Send(messageId, chatId, authorId, MessageContent.Create("first"), [Guid.NewGuid()]);
        var act = () => repo.AddAsync(retry);

        await act.Should().NotThrowAsync();

        var loaded = await repo.GetByIdAsync(messageId);
        loaded!.Content.Text.Should().Be("first", "the original insert must win, not be overwritten by the retry");
    }

    [Fact]
    public async Task Concurrent_loads_both_mutate_second_UpdateAsync_throws_ConcurrencyConflictException()
    {
        var repo = NewRepository();
        var message = NewMessage();
        await repo.AddAsync(message);

        // Two independent readers load the same version...
        var copyA = await repo.GetByIdAsync(message.Id);
        var copyB = await repo.GetByIdAsync(message.Id);

        copyA!.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);
        copyB!.AddReaction(Guid.NewGuid(), Emoji.Heart, isPremium: false);

        // ...first writer wins...
        await repo.UpdateAsync(copyA);

        // ...second writer's write is based on a stale version and must conflict.
        var act = () => repo.UpdateAsync(copyB);
        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task UpdateAsync_after_conflict_can_be_retried_by_reloading()
    {
        var repo = NewRepository();
        var message = NewMessage();
        await repo.AddAsync(message);

        var copyA = await repo.GetByIdAsync(message.Id);
        var copyB = await repo.GetByIdAsync(message.Id);
        copyA!.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);
        copyB!.AddReaction(Guid.NewGuid(), Emoji.Heart, isPremium: false);
        await repo.UpdateAsync(copyA);

        var reloaded = await repo.GetByIdAsync(message.Id);
        reloaded!.AddReaction(Guid.NewGuid(), Emoji.Wow, isPremium: false);
        await repo.UpdateAsync(reloaded);

        var final = await repo.GetByIdAsync(message.Id);
        final!.Reactions.Should().HaveCount(2, "the reload-and-retry recovers both writes without losing the first");
    }

    [Fact]
    public async Task IncrementBroadcastReadCountAsync_accumulates_atomically()
    {
        var repo = NewRepository();
        var message = NewMessage(isBroadcast: true);
        await repo.AddAsync(message);

        const int readers = 20;
        await Task.WhenAll(Enumerable.Range(0, readers).Select(_ => repo.IncrementBroadcastReadCountAsync(message.Id)));

        var loaded = await repo.GetByIdAsync(message.Id);
        loaded!.BroadcastReadCount.Should().Be(readers, "concurrent atomic $inc must not lose any increment");
    }
}
