using TelegramLike.Contracts.Chats;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Membership;
using FluentAssertions;

namespace TelegramLike.Realtime.Tests.Consumers;

/// <summary>
/// These consumers only update ChatMembershipTracker (the in-memory authorization
/// view backing JoinChat) — they push nothing to hub groups. Exercised against the
/// real tracker rather than a mock so both the wiring and the tracker semantics are
/// verified together.
/// </summary>
public class MembershipConsumersTests
{
    [Fact]
    public async Task MemberJoinedMembershipConsumer_AddsUserToTracker()
    {
        var tracker = new ChatMembershipTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new MemberJoinedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId, [userId]);

        var consumer = new MemberJoinedMembershipConsumer(tracker);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        tracker.IsMember(chatId, userId).Should().BeTrue();
    }

    [Fact]
    public async Task MemberLeftMembershipConsumer_RemovesUserFromTracker()
    {
        var tracker = new ChatMembershipTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        tracker.Join(chatId, userId);
        var evt = new MemberLeftIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId);

        var consumer = new MemberLeftMembershipConsumer(tracker);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        tracker.IsMember(chatId, userId).Should().BeFalse();
    }

    [Fact]
    public async Task MemberKickedMembershipConsumer_RemovesUserFromTracker()
    {
        var tracker = new ChatMembershipTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        tracker.Join(chatId, userId);
        var evt = new MemberKickedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId, Guid.NewGuid(), [userId]);

        var consumer = new MemberKickedMembershipConsumer(tracker);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        tracker.IsMember(chatId, userId).Should().BeFalse();
    }

    [Fact]
    public async Task ChatMembershipsSnapshotConsumer_MaterializesAllMembers_MakingChatKnownAndFailClosed()
    {
        var tracker = new ChatMembershipTracker();
        var chatId = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var evt = new ChatMembershipsSnapshotIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId,
        [
            new ChatMembershipSnapshotEntry(memberA, "Owner", DateTime.UtcNow),
            new ChatMembershipSnapshotEntry(memberB, "Member", DateTime.UtcNow),
        ]);

        var consumer = new ChatMembershipsSnapshotMembershipConsumer(tracker);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        // The chat is now "known", so JoinChat is fail-closed: members allowed, outsider rejected.
        tracker.IsKnownChat(chatId).Should().BeTrue();
        tracker.IsMember(chatId, memberA).Should().BeTrue();
        tracker.IsMember(chatId, memberB).Should().BeTrue();
        tracker.IsMember(chatId, outsider).Should().BeFalse();
    }

    [Fact]
    public async Task MemberJoinedMembershipConsumer_DuplicateDelivery_IsIdempotent()
    {
        var tracker = new ChatMembershipTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new MemberJoinedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId, [userId]);
        var consumer = new MemberJoinedMembershipConsumer(tracker);

        await consumer.Consume(HubTestDoubles.ContextFor(evt));
        await consumer.Consume(HubTestDoubles.ContextFor(evt)); // redelivery

        tracker.IsMember(chatId, userId).Should().BeTrue();
    }
}
