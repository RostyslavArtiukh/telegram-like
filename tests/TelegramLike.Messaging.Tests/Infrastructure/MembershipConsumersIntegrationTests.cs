using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Infrastructure.Messaging.Consumers;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

// The consumers are thin pass-throughs onto IChatMembershipReadModel; exercising them
// against the real Mongo-backed read model (rather than a mocked interface) verifies
// both the wiring (right method, right arguments) and that RabbitMQ's at-least-once
// redelivery is safe end-to-end: a duplicate delivery must leave the read model in the
// same state as a single delivery.
[Collection(MongoCollection.Name)]
public class MembershipConsumersIntegrationTests(MongoFixture fx)
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private MongoChatMembershipReadModel NewReadModel() => new(fx.Database);

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task MemberJoinedConsumer_DuplicateDelivery_LeavesMemberActiveOnce()
    {
        var readModel = NewReadModel();
        var consumer = new MemberJoinedConsumer(readModel);
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new MemberJoinedIntegrationEvent(Guid.NewGuid(), T0, chatId, userId, [userId], "Member");

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeTrue();
        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().ContainSingle().Which.Should().Be(userId);
    }

    [Fact]
    public async Task MemberLeftConsumer_DuplicateDelivery_LeavesMemberInactiveOnce()
    {
        var readModel = NewReadModel();
        await readModel.UpsertActiveAsync(Guid.NewGuid(), Guid.NewGuid(), "Member", T0); // noise
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberLeftConsumer(readModel);
        var evt = new MemberLeftIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task MemberKickedConsumer_DuplicateDelivery_LeavesMemberInactiveOnce()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberKickedConsumer(readModel);
        var evt = new MemberKickedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId, Guid.NewGuid(), [userId]);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task MemberRoleChangedConsumer_DuplicateDelivery_LeavesRoleAppliedOnce()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberRoleChangedConsumer(readModel);
        var evt = new MemberRoleChangedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId, "Admin");

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsModeratorAsync(chatId, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task MemberRoleChangedConsumer_StaleRedelivery_DoesNotRevertNewerRole()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberRoleChangedConsumer(readModel);
        var promote = new MemberRoleChangedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(10), chatId, userId, "Admin");
        var staleDemote = new MemberRoleChangedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId, "Member");

        await consumer.Consume(ContextFor(promote));
        await consumer.Consume(ContextFor(staleDemote)); // out-of-order redelivery

        (await readModel.IsModeratorAsync(chatId, userId)).Should().BeTrue(
            "a stale role event delivered after a newer one must not revert the role");
    }

    // ── [TL-101] backfill snapshot consumer ──────────────────────────────────

    [Fact]
    public async Task ChatMembershipsSnapshotConsumer_MaterializesAllActiveMembers_Idempotently()
    {
        var readModel = NewReadModel();
        var consumer = new ChatMembershipsSnapshotConsumer(readModel);
        var chatId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var evt = new ChatMembershipsSnapshotIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(60), chatId,
        [
            new ChatMembershipSnapshotEntry(owner, "Owner", T0),
            new ChatMembershipSnapshotEntry(member, "Member", T0),
        ]);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery must be a no-op

        (await readModel.IsActiveMemberAsync(chatId, owner)).Should().BeTrue();
        (await readModel.IsActiveMemberAsync(chatId, member)).Should().BeTrue();
        (await readModel.IsModeratorAsync(chatId, owner)).Should().BeTrue("the snapshot carries the Owner role");
        (await readModel.IsModeratorAsync(chatId, member)).Should().BeFalse();
        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().BeEquivalentTo(new[] { owner, member });
    }

    // ── Ban and chat deletion must actually revoke access here ────────────

    [Fact]
    public async Task MemberBannedConsumer_DeactivatesTheMember()
    {
        // The whole point of publishing MemberBanned: Messaging decides membership from this
        // read-model, so without it a banned user would keep sending messages and reacting.
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var bannedUser = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, owner, "Owner", T0);
        await readModel.UpsertActiveAsync(chatId, bannedUser, "Member", T0);

        var consumer = new MemberBannedConsumer(readModel);
        var evt = new MemberBannedIntegrationEvent(
            Guid.NewGuid(), T0.AddSeconds(10), chatId, bannedUser, owner, "spam");

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery must be a no-op

        (await readModel.IsActiveMemberAsync(chatId, bannedUser)).Should().BeFalse();
        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().ContainSingle().Which.Should().Be(owner);
    }

    [Fact]
    public async Task MemberBannedConsumer_StaleRedelivery_DoesNotUndoALaterRejoinOfSomeoneElse()
    {
        // LWW guard: an old ban redelivered after newer membership traffic must not win.
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0.AddSeconds(60));

        var consumer = new MemberBannedConsumer(readModel);
        await consumer.Consume(ContextFor(
            new MemberBannedIntegrationEvent(Guid.NewGuid(), T0, chatId, userId, Guid.NewGuid(), null)));

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeTrue(
            "the ban is older than the membership state on record");
    }

    [Fact]
    public async Task ChatDeletedConsumer_DeactivatesEveryMemberOfTheChat()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, owner, "Owner", T0);
        await readModel.UpsertActiveAsync(chatId, memberA, "Member", T0);
        await readModel.UpsertActiveAsync(chatId, memberB, "Member", T0);

        var consumer = new ChatDeletedConsumer(readModel);
        var evt = new ChatDeletedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(10), chatId, owner);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery must be a no-op

        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().BeEmpty();
        (await readModel.IsActiveMemberAsync(chatId, owner)).Should().BeFalse();
        (await readModel.IsModeratorAsync(chatId, owner)).Should().BeFalse(
            "an inactive row must not keep moderator authority in a deleted chat");
    }

    [Fact]
    public async Task ChatDeletedConsumer_LeavesOtherChatsUntouched()
    {
        var readModel = NewReadModel();
        var deletedChat = Guid.NewGuid();
        var survivingChat = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(deletedChat, userId, "Member", T0);
        await readModel.UpsertActiveAsync(survivingChat, userId, "Member", T0);

        await new ChatDeletedConsumer(readModel).Consume(ContextFor(
            new ChatDeletedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(10), deletedChat, Guid.NewGuid())));

        (await readModel.IsActiveMemberAsync(deletedChat, userId)).Should().BeFalse();
        (await readModel.IsActiveMemberAsync(survivingChat, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task ChatDeletedConsumer_LeavesTheChatKnown_SoAccessChecksStayFailClosed()
    {
        // The trap this closes: handlers used to read "no active members" as "chat unknown",
        // which is the fail-OPEN branch. Deleting a chat empties its active members, so without
        // IsChatKnown a deleted chat would start accepting messages from anyone again.
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, owner, "Owner", T0);

        await new ChatDeletedConsumer(readModel).Consume(ContextFor(
            new ChatDeletedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(10), chatId, owner)));

        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().BeEmpty();
        (await readModel.IsChatKnownAsync(chatId)).Should().BeTrue(
            "the chat must stay known so access checks fail closed rather than open");
    }

    [Fact]
    public async Task MemberBannedConsumer_OfTheLastMember_LeavesTheChatKnown()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        await new MemberBannedConsumer(readModel).Consume(ContextFor(
            new MemberBannedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(10), chatId, userId, Guid.NewGuid(), null)));

        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().BeEmpty();
        (await readModel.IsChatKnownAsync(chatId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsChatKnown_ForAChatNeverSeen_IsFalse()
    {
        // Preserves the deliberate fail-open window for a chat whose MemberJoined is still
        // in flight — that is the one case the handlers may fall through.
        var readModel = NewReadModel();

        (await readModel.IsChatKnownAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task ChatDeletedConsumer_ForAChatItNeverMaterialized_CreatesNothing()
    {
        // UpdateMany without upsert: an unknown chat has no membership to revoke, and
        // inventing rows for it would be meaningless.
        var readModel = NewReadModel();
        var unknownChat = Guid.NewGuid();

        await new ChatDeletedConsumer(readModel).Consume(ContextFor(
            new ChatDeletedIntegrationEvent(Guid.NewGuid(), T0, unknownChat, Guid.NewGuid())));

        (await readModel.GetActiveMemberIdsAsync(unknownChat)).Should().BeEmpty();
        (await readModel.IsChatKnownAsync(unknownChat)).Should().BeFalse(
            "an UpdateMany without upsert must not conjure rows for a chat we never knew");
    }

    [Fact]
    public async Task ChatMembershipsSnapshotConsumer_StaleSnapshot_DoesNotResurrectLeftMember()
    {
        // The snapshot carries each member's original JoinedAt. If a live MemberLeft has already
        // been processed (newer timestamp), the backfill must NOT resurrect the departed member.
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);
        await readModel.DeactivateAsync(chatId, userId, T0.AddSeconds(10)); // they left after joining

        var consumer = new ChatMembershipsSnapshotConsumer(readModel);
        var evt = new ChatMembershipsSnapshotIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(60), chatId,
            [new ChatMembershipSnapshotEntry(userId, "Member", T0)]); // JoinedAt = T0, older than the leave

        await consumer.Consume(ContextFor(evt));

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeFalse(
            "the snapshot's JoinedAt is older than the processed leave, so LWW must not resurrect the member");
    }
}
