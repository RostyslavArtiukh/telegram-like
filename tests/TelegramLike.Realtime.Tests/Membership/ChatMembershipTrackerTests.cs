using FluentAssertions;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Tests.Membership;

public class ChatMembershipTrackerTests
{
    private static ChatMembershipTracker NewTracker() => new();

    [Fact]
    public void UnknownChat_IsNotKnownAndHasNoMembers()
    {
        var tracker = NewTracker();

        tracker.IsKnownChat(Guid.NewGuid()).Should().BeFalse();
        tracker.IsMember(Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Join_MakesChatKnownAndUserAMember()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.Join(chatId, userId);

        tracker.IsKnownChat(chatId).Should().BeTrue();
        tracker.IsMember(chatId, userId).Should().BeTrue();
    }

    [Fact]
    public void Join_DoesNotMakeOtherUsersMembers()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        tracker.Join(chatId, Guid.NewGuid());

        tracker.IsMember(chatId, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Join_Duplicate_IsIdempotent()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.Join(chatId, userId);
        tracker.Join(chatId, userId);

        tracker.IsMember(chatId, userId).Should().BeTrue();
    }

    [Fact]
    public void Leave_RemovesMembershipButChatStaysKnown()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        tracker.Join(chatId, userId);

        tracker.Leave(chatId, userId);

        tracker.IsMember(chatId, userId).Should().BeFalse();
        tracker.IsKnownChat(chatId).Should().BeTrue("the chat itself remains known once any event for it has been seen");
    }

    [Fact]
    public void Leave_OnUnknownChat_DoesNotThrow()
    {
        var tracker = NewTracker();

        var act = () => tracker.Leave(Guid.NewGuid(), Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Leave_UserWhoNeverJoined_IsNoop()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        tracker.Join(chatId, Guid.NewGuid());

        var act = () => tracker.Leave(chatId, Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void DifferentChats_AreIsolated()
    {
        var tracker = NewTracker();
        var userId = Guid.NewGuid();
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();

        tracker.Join(chatA, userId);

        tracker.IsMember(chatA, userId).Should().BeTrue();
        tracker.IsMember(chatB, userId).Should().BeFalse();
        tracker.IsKnownChat(chatB).Should().BeFalse();
    }

    [Fact]
    public void Rejoin_AfterLeave_RestoresMembership()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        tracker.Join(chatId, userId);
        tracker.Leave(chatId, userId);

        tracker.Join(chatId, userId);

        tracker.IsMember(chatId, userId).Should().BeTrue();
    }
}
