using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Tests.Membership;

/// <summary>
/// JoinChat's authorization. What this replaced mattered as much as what it does: a replica
/// used to answer only from membership it had happened to observe on the bus, waving through
/// anything it hadn't — which, after a restart, was every chat in the system until someone
/// re-ran the admin backfill by hand ([TL-127]).
/// </summary>
public class ChatMembershipCheckTests
{
    private readonly IChatMembershipSource _chats = Substitute.For<IChatMembershipSource>();

    private ChatMembershipCheck NewCheck() => new(_chats, NullLogger<ChatMembershipCheck>.Instance);

    [Fact]
    public async Task AChatItHasNeverSeen_IsAskedAbout_NotWavedThrough()
    {
        var check = NewCheck();
        var chatId = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        _chats.IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>()).Returns(false);

        var mayJoin = await check.MayJoinAsync(chatId, outsider, "token");

        mayJoin.Should().BeFalse();
        await _chats.Received(1).IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMemberOfAChatItHasNeverSeen_IsLetIn()
    {
        var check = NewCheck();
        var chatId = Guid.NewGuid();
        var member = Guid.NewGuid();
        _chats.IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>()).Returns(true);

        (await check.MayJoinAsync(chatId, member, "token")).Should().BeTrue();
    }

    [Fact]
    public async Task AnAnswerIsAskedForOnce_ThenRemembered()
    {
        // A join is on the hot path of opening a chat; asking Chats every time would put a
        // network round-trip in front of it.
        var check = NewCheck();
        var chatId = Guid.NewGuid();
        var member = Guid.NewGuid();
        _chats.IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>()).Returns(true);

        await check.MayJoinAsync(chatId, member, "token");
        await check.MayJoinAsync(chatId, member, "token");
        await check.MayJoinAsync(chatId, member, "token");

        await _chats.Received(1).IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANoIsRememberedToo_SoARejectedJoinCannotBeRetriedIntoALoadOnChats()
    {
        var check = NewCheck();
        var chatId = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        _chats.IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>()).Returns(false);

        await check.MayJoinAsync(chatId, outsider, "token");
        (await check.MayJoinAsync(chatId, outsider, "token")).Should().BeFalse();

        await _chats.Received(1).IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenChatsCannotBeReached_TheJoinIsAllowed_ButNothingIsRemembered()
    {
        // Fail-open survives, deliberately: pushes are id-only and content stays behind
        // Messaging's fail-closed reads, so an outage should not silence the push channel.
        // The difference from before is that it is now a transient, logged condition rather
        // than the standing state of every chat a replica has not observed — and an unknown
        // answer must not be cached, or the outage would outlive itself.
        var check = NewCheck();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _chats.IsMemberAsync(chatId, "token", Arg.Any<CancellationToken>()).Returns((bool?)null);

        var mayJoin = await check.MayJoinAsync(chatId, userId, "token");

        mayJoin.Should().BeTrue();
        check.RememberedAnswers.Should().Be(0);
    }

    [Fact]
    public async Task WithNoAccessTokenOnTheConnection_TheJoinIsAllowed_WithoutAskingChats()
    {
        // The connection authenticated, so a token existed at handshake; not finding one here
        // means the check simply cannot run — not that the user is an outsider.
        var check = NewCheck();

        var mayJoin = await check.MayJoinAsync(Guid.NewGuid(), Guid.NewGuid(), accessToken: null);

        mayJoin.Should().BeTrue();
        await _chats.DidNotReceive().IsMemberAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RefreshDoesNotInventAnAnswer()
    {
        var check = NewCheck();

        check.Refresh(Guid.NewGuid(), Guid.NewGuid(), isMember: true);

        check.RememberedAnswers.Should().Be(0);
    }

    [Fact]
    public async Task MemoryFollowsThePairsThisReplicaWasActuallyAskedAbout()
    {
        // The ceiling this replaced: every replica held every membership in the system.
        var check = NewCheck();
        _chats.IsMemberAsync(Arg.Any<Guid>(), "token", Arg.Any<CancellationToken>()).Returns(true);

        foreach (var _ in Enumerable.Range(0, 5))
            await check.MayJoinAsync(Guid.NewGuid(), Guid.NewGuid(), "token");

        check.RememberedAnswers.Should().Be(5);
    }
}
