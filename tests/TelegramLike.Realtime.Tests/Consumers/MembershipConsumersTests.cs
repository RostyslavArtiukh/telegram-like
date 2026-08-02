using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Contracts.Chats;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Tests.Consumers;

/// <summary>
/// These consumers only keep <see cref="ChatMembershipCheck"/>'s answers fresh — they push
/// nothing to hub groups. Exercised against the real check (with a stub Chats) rather than a
/// mock, so the wiring and the refresh-don't-materialize rule are verified together.
/// </summary>
public class MembershipConsumersTests
{
    private readonly IChatMembershipSource _chats = Substitute.For<IChatMembershipSource>();

    private ChatMembershipCheck NewCheck() =>
        new(_chats, NullLogger<ChatMembershipCheck>.Instance);

    /// Puts a real, asked-for answer in the check, the way a first JoinChat would.
    private async Task<ChatMembershipCheck> CheckKnowing(params (Guid ChatId, Guid UserId)[] members)
    {
        var check = NewCheck();
        foreach (var (chatId, userId) in members)
        {
            _chats.IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>()).Returns(true);
            await check.MayJoinAsync(chatId, userId, "token");
        }

        return check;
    }

    [Fact]
    public async Task MemberLeft_TurnsAKnownYesIntoANo()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var check = await CheckKnowing((chatId, userId));
        var evt = new MemberLeftIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId);

        await new MemberLeftMembershipConsumer(check).Consume(HubTestDoubles.ContextFor(evt));

        (await check.MayJoinAsync(chatId, userId, "token")).Should().BeFalse();
    }

    [Fact]
    public async Task MemberKicked_TurnsAKnownYesIntoANo()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var check = await CheckKnowing((chatId, userId));
        var evt = new MemberKickedIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, chatId, userId, Guid.NewGuid(), [userId]);

        await new MemberKickedMembershipConsumer(check).Consume(HubTestDoubles.ContextFor(evt));

        (await check.MayJoinAsync(chatId, userId, "token")).Should().BeFalse();
    }

    [Fact]
    public async Task MemberBanned_TurnsAKnownYesIntoANo()
    {
        // Stops a banned member re-subscribing to the chat's live push group.
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var check = await CheckKnowing((chatId, userId));
        var evt = new MemberBannedIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, chatId, userId, Guid.NewGuid(), "spam");

        await new MemberBannedMembershipConsumer(check).Consume(HubTestDoubles.ContextFor(evt));

        (await check.MayJoinAsync(chatId, userId, "token")).Should().BeFalse();
    }

    [Fact]
    public async Task MemberJoined_DoesNotCacheAPairNobodyHereAskedAbout()
    {
        // The rule that keeps this replica's memory proportional to its own connections
        // instead of to every membership in the system: events refresh, they never
        // materialize. The pair still resolves — by asking Chats on first use.
        var check = NewCheck();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new MemberJoinedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId, [userId]);

        await new MemberJoinedMembershipConsumer(check).Consume(HubTestDoubles.ContextFor(evt));

        check.RememberedAnswers.Should().Be(0);
    }

    [Fact]
    public async Task MemberJoined_RevivesAKnownNo_SoARejoinIsNotStuckOnAStaleAnswer()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var check = await CheckKnowing((chatId, userId));
        await new MemberLeftMembershipConsumer(check)
            .Consume(HubTestDoubles.ContextFor(new MemberLeftIntegrationEvent(
                Guid.NewGuid(), DateTime.UtcNow, chatId, userId)));

        var rejoined = new MemberJoinedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId, [userId]);
        await new MemberJoinedMembershipConsumer(check).Consume(HubTestDoubles.ContextFor(rejoined));

        (await check.MayJoinAsync(chatId, userId, "token")).Should().BeTrue();
    }

    [Fact]
    public async Task ChatDeleted_RevokesEveryAnswerHeldForThatChat()
    {
        // Load-bearing, not belt-and-braces: Chats' own member lookup ignores DeletedAt, so
        // asking it about a deleted chat still says "member". This event is the only revoke.
        var chatId = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var check = await CheckKnowing((chatId, memberA), (chatId, memberB));
        var evt = new ChatDeletedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, memberA);

        await new ChatDeletedMembershipConsumer(check).Consume(HubTestDoubles.ContextFor(evt));

        (await check.MayJoinAsync(chatId, memberA, "token")).Should().BeFalse();
        (await check.MayJoinAsync(chatId, memberB, "token")).Should().BeFalse();
    }

    [Fact]
    public async Task ARedeliveredEvent_ChangesNothing()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var check = await CheckKnowing((chatId, userId));
        var evt = new MemberLeftIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId);
        var consumer = new MemberLeftMembershipConsumer(check);

        await consumer.Consume(HubTestDoubles.ContextFor(evt));
        await consumer.Consume(HubTestDoubles.ContextFor(evt)); // redelivery

        (await check.MayJoinAsync(chatId, userId, "token")).Should().BeFalse();
        check.RememberedAnswers.Should().Be(1);
    }
}
