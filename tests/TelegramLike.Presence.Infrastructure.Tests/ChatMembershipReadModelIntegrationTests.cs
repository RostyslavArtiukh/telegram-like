using FluentAssertions;
using TelegramLike.Presence.Infrastructure.Persistence;
using TelegramLike.Presence.Infrastructure.Tests.Fixtures;

namespace TelegramLike.Presence.Infrastructure.Tests;

[Collection(MongoCollection.Name)]
public class ChatMembershipReadModelIntegrationTests(MongoFixture fx)
{
    private MongoChatMembershipReadModel NewReadModel() => new(fx.Database);

    [Fact]
    public async Task Upsert_then_IsActiveMember_returns_true()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId);

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveMember_returns_false_for_unknown_pair()
    {
        var sut = NewReadModel();

        (await sut.IsActiveMemberAsync(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_is_idempotent_under_redelivery()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId);
        await sut.UpsertActiveAsync(chatId, userId);
        await sut.UpsertActiveAsync(chatId, userId);

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue(
            "RabbitMQ redeliveries must not corrupt the read model");
    }

    [Fact]
    public async Task Remove_after_Upsert_makes_member_inactive()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId);
        await sut.RemoveAsync(chatId, userId);

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_on_unknown_pair_is_noop()
    {
        var sut = NewReadModel();

        var act = () => sut.RemoveAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Different_chats_are_isolated()
    {
        var sut = NewReadModel();
        var userId = Guid.NewGuid();
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatA, userId);

        (await sut.IsActiveMemberAsync(chatA, userId)).Should().BeTrue();
        (await sut.IsActiveMemberAsync(chatB, userId)).Should().BeFalse(
            "membership in one chat must not imply membership in another");
    }
}
