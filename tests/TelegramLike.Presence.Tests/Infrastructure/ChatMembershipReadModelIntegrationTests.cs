using FluentAssertions;
using TelegramLike.Presence.Infrastructure.Storage;
using TelegramLike.Presence.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Presence.Tests.Infrastructure;

[Collection(MongoCollection.Name)]
public class ChatMembershipReadModelIntegrationTests(MongoFixture fx)
{
    private MongoChatMembershipReadModel NewReadModel() => new(fx.Database);
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Upsert_ThenIsActiveMember_ReturnsTrue()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId, T0);

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveMember_UnknownPair_ReturnsFalse()
    {
        var sut = NewReadModel();

        (await sut.IsActiveMemberAsync(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_UnderRedelivery_IsIdempotent()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId, T0);
        await sut.UpsertActiveAsync(chatId, userId, T0);
        await sut.UpsertActiveAsync(chatId, userId, T0);

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue(
            "RabbitMQ redeliveries must not corrupt the read model");
    }

    [Fact]
    public async Task Deactivate_AfterUpsert_MakesMemberInactive()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId, T0);
        await sut.DeactivateAsync(chatId, userId, T0.AddSeconds(1));

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_OnUnknownPair_IsNoop()
    {
        var sut = NewReadModel();

        var act = () => sut.DeactivateAsync(Guid.NewGuid(), Guid.NewGuid(), T0);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DifferentChats_AreIsolated()
    {
        var sut = NewReadModel();
        var userId = Guid.NewGuid();
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatA, userId, T0);

        (await sut.IsActiveMemberAsync(chatA, userId)).Should().BeTrue();
        (await sut.IsActiveMemberAsync(chatB, userId)).Should().BeFalse(
            "membership in one chat must not imply membership in another");
    }

    [Fact]
    public async Task StaleJoinRedeliveredAfterLeave_DoesNotResurrectMembership()
    {
        // The out-of-order case B8 guards: a leave at T1 is processed, then an older
        // join (T0 < T1) is redelivered. Last-writer-wins by occurredAt must keep the
        // member inactive rather than let the stale join re-add them.
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId, T0);
        await sut.DeactivateAsync(chatId, userId, T0.AddSeconds(5));
        await sut.UpsertActiveAsync(chatId, userId, T0); // stale redelivery

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeFalse(
            "a join older than the last leave must not resurrect the membership");
    }

    [Fact]
    public async Task StaleLeaveRedeliveredAfterRejoin_DoesNotRemoveMember()
    {
        // Mirror case: kick/leave at T0, genuine rejoin at T1, then the old leave is
        // redelivered. The newer rejoin must win.
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.DeactivateAsync(chatId, userId, T0);
        await sut.UpsertActiveAsync(chatId, userId, T0.AddSeconds(5));
        await sut.DeactivateAsync(chatId, userId, T0); // stale redelivery

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue(
            "a leave older than the last rejoin must not remove the active member");
    }
}
