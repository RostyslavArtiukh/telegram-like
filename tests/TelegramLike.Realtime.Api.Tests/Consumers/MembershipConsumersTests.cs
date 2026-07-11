using TelegramLike.Contracts.Chats;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Membership;
using FluentAssertions;

namespace TelegramLike.Realtime.Api.Tests.Consumers;

/// <summary>
/// These consumers only update ChatMembershipTracker (the in-memory authorization
/// view backing JoinChat) — they push nothing to hub groups. Exercised against the
/// real tracker rather than a mock so both the wiring and the tracker semantics are
/// verified together.
/// </summary>
public class MembershipConsumersTests
{
    [Fact]
    public async Task MemberJoinedMembershipConsumer_adds_the_user_to_the_tracker()
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
    public async Task MemberLeftMembershipConsumer_removes_the_user_from_the_tracker()
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
    public async Task MemberKickedMembershipConsumer_removes_the_user_from_the_tracker()
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
    public async Task Duplicate_MemberJoined_delivery_is_idempotent()
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
