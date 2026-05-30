using FluentAssertions;
using TelegramLike.Domain.Messaging.Aggregates;
using TelegramLike.Domain.Messaging.Events;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Domain.Tests.Messaging;

public class MessageTests
{
    private static MessageContent Hello => MessageContent.Create("hello");

    [Fact]
    public void Send_creates_active_message_and_raises_event()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var msg = Message.Send(chatId, authorId, Hello, []);

        msg.ChatId.Should().Be(chatId);
        msg.AuthorId.Should().Be(authorId);
        msg.Status.IsRetracted.Should().BeFalse();
        msg.BroadcastReadCount.Should().BeNull();
        msg.DomainEvents.OfType<MessageSentEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Send_to_broadcast_initializes_read_count_to_zero()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, [], isBroadcast: true);

        msg.BroadcastReadCount.Should().Be(0);
    }

    [Fact]
    public void Retract_by_author_replaces_content()
    {
        var author = Guid.NewGuid();
        var msg = Message.Send(Guid.NewGuid(), author, Hello, []);

        msg.Retract(author, isAuthorOrModerator: true);

        msg.IsRetracted.Should().BeTrue();
        msg.Content.Text.Should().Be("[retracted]");
        msg.DomainEvents.OfType<MessageRetractedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Retract_by_non_author_non_moderator_throws()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, []);

        var act = () => msg.Retract(Guid.NewGuid(), isAuthorOrModerator: false);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retract_twice_throws()
    {
        var author = Guid.NewGuid();
        var msg = Message.Send(Guid.NewGuid(), author, Hello, []);
        msg.Retract(author, true);

        var act = () => msg.Retract(author, true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddReaction_free_user_limited_to_one()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, []);
        var user = Guid.NewGuid();
        msg.AddReaction(user, Emoji.Heart, isPremium: false);

        var act = () => msg.AddReaction(user, Emoji.Fire, isPremium: false);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddReaction_premium_user_allowed_two_distinct_emojis()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, []);
        var user = Guid.NewGuid();
        msg.AddReaction(user, Emoji.Heart, isPremium: true);
        msg.AddReaction(user, Emoji.Fire, isPremium: true);

        msg.Reactions.Where(r => r.UserId == user).Should().HaveCount(2);
    }

    [Fact]
    public void AddReaction_same_emoji_twice_throws()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, []);
        var user = Guid.NewGuid();
        msg.AddReaction(user, Emoji.Heart, isPremium: true);

        var act = () => msg.AddReaction(user, Emoji.Heart, isPremium: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveReaction_drops_it_and_raises_event()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, []);
        var user = Guid.NewGuid();
        msg.AddReaction(user, Emoji.Heart, false);

        msg.RemoveReaction(user, Emoji.Heart);

        msg.Reactions.Should().BeEmpty();
        msg.DomainEvents.OfType<ReactionRemovedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void IncrementBroadcastReadCount_throws_for_non_broadcast()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, []);

        var act = () => msg.IncrementBroadcastReadCount();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IncrementBroadcastReadCount_bumps_counter()
    {
        var msg = Message.Send(Guid.NewGuid(), Guid.NewGuid(), Hello, [], isBroadcast: true);

        msg.IncrementBroadcastReadCount();
        msg.IncrementBroadcastReadCount();

        msg.BroadcastReadCount.Should().Be(2);
    }

    [Fact]
    public void MessageContent_requires_text_or_attachments()
    {
        var act = () => MessageContent.Create(null);
        act.Should().Throw<ArgumentException>();
    }
}
