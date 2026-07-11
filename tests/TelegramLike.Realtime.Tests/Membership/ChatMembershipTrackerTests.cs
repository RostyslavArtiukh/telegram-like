using FluentAssertions;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Tests.Membership;

public class ChatMembershipTrackerTests
{
    private static ChatMembershipTracker NewTracker() => new();

    [Fact]
    public void Unknown_chat_is_not_known_and_has_no_members()
    {
        var tracker = NewTracker();

        tracker.IsKnownChat(Guid.NewGuid()).Should().BeFalse();
        tracker.IsMember(Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Join_makes_the_chat_known_and_the_user_a_member()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.Join(chatId, userId);

        tracker.IsKnownChat(chatId).Should().BeTrue();
        tracker.IsMember(chatId, userId).Should().BeTrue();
    }

    [Fact]
    public void Join_does_not_make_other_users_members_of_the_same_chat()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        tracker.Join(chatId, Guid.NewGuid());

        tracker.IsMember(chatId, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Duplicate_Join_is_idempotent()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.Join(chatId, userId);
        tracker.Join(chatId, userId);

        tracker.IsMember(chatId, userId).Should().BeTrue();
    }

    [Fact]
    public void Leave_removes_membership_but_the_chat_stays_known()
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
    public void Leave_on_an_unknown_chat_does_not_throw()
    {
        var tracker = NewTracker();

        var act = () => tracker.Leave(Guid.NewGuid(), Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Leave_for_a_user_who_never_joined_is_a_noop()
    {
        var tracker = NewTracker();
        var chatId = Guid.NewGuid();
        tracker.Join(chatId, Guid.NewGuid());

        var act = () => tracker.Leave(chatId, Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Different_chats_are_isolated()
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
    public void Rejoin_after_leave_restores_membership()
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
