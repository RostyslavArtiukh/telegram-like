using FluentAssertions;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Events;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Tests.Domain;

public class MessageTests
{
    private static Message NewMessage(
        Guid? chatId = null,
        Guid? authorId = null,
        IReadOnlyList<Guid>? recipients = null,
        ReplyReference? replyTo = null,
        ForwardReference? forwardFrom = null,
        bool isBroadcast = false)
        => Message.Send(
            Guid.NewGuid(),
            chatId ?? Guid.NewGuid(),
            authorId ?? Guid.NewGuid(),
            MessageContent.Create("hello"),
            recipients ?? [Guid.NewGuid()],
            replyTo,
            forwardFrom,
            isBroadcast);

    [Fact]
    public void Send_with_empty_messageId_throws()
    {
        var act = () => Message.Send(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), MessageContent.Create("hi"), [Guid.NewGuid()]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_with_empty_chatId_throws()
    {
        var act = () => Message.Send(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), MessageContent.Create("hi"), [Guid.NewGuid()]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_with_empty_authorId_throws()
    {
        var act = () => Message.Send(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, MessageContent.Create("hi"), [Guid.NewGuid()]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_with_null_recipients_throws()
    {
        var act = () => Message.Send(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MessageContent.Create("hi"), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Send_raises_MessageSentEvent_with_recipients()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var recipients = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var message = NewMessage(chatId, authorId, recipients);

        var evt = message.PendingEvents.OfType<MessageSentEvent>().Should().ContainSingle().Subject;
        evt.ChatId.Should().Be(chatId);
        evt.AuthorId.Should().Be(authorId);
        evt.Recipients.Should().BeEquivalentTo(recipients);
    }

    [Fact]
    public void Send_non_broadcast_has_null_broadcast_read_count()
    {
        var message = NewMessage(isBroadcast: false);

        message.BroadcastReadCount.Should().BeNull();
    }

    [Fact]
    public void Send_broadcast_starts_broadcast_read_count_at_zero()
    {
        var message = NewMessage(isBroadcast: true);

        message.BroadcastReadCount.Should().Be(0);
    }

    [Fact]
    public void Send_with_reply_carries_reply_reference_in_event()
    {
        var replyToId = Guid.NewGuid();
        var message = NewMessage(replyTo: ReplyReference.To(replyToId));

        message.ReplyTo!.ReplyToMessageId.Should().Be(replyToId);
        message.PendingEvents.OfType<MessageSentEvent>().Single().ReplyToMessageId.Should().Be(replyToId);
    }

    [Fact]
    public void Send_with_forward_carries_forward_reference_in_event()
    {
        var originalMessageId = Guid.NewGuid();
        var originalChatId = Guid.NewGuid();
        var message = NewMessage(forwardFrom: ForwardReference.From(originalMessageId, originalChatId));

        message.ForwardFrom!.OriginalMessageId.Should().Be(originalMessageId);
        message.ForwardFrom!.OriginalChatId.Should().Be(originalChatId);
        message.PendingEvents.OfType<MessageSentEvent>().Single().ForwardOriginalMessageId.Should().Be(originalMessageId);
    }

    [Fact]
    public void ReplyReference_To_with_empty_id_throws()
    {
        var act = () => ReplyReference.To(Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ForwardReference_From_with_empty_messageId_throws()
    {
        var act = () => ForwardReference.From(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ForwardReference_From_with_empty_chatId_throws()
    {
        var act = () => ForwardReference.From(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    // ---- Retract ----

    [Fact]
    public void Retract_by_author_or_moderator_marks_retracted_and_raises_event()
    {
        var message = NewMessage();
        var retractedBy = Guid.NewGuid();

        message.Retract(retractedBy, isAuthorOrModerator: true);

        message.IsRetracted.Should().BeTrue();
        message.Content.Text.Should().Be("[retracted]");
        message.PendingEvents.OfType<MessageRetractedEvent>().Should().ContainSingle()
            .Which.RetractedBy.Should().Be(retractedBy);
    }

    [Fact]
    public void Retract_without_authorization_throws_and_does_not_mutate()
    {
        var message = NewMessage();

        var act = () => message.Retract(Guid.NewGuid(), isAuthorOrModerator: false);

        act.Should().Throw<DomainException>();
        message.IsRetracted.Should().BeFalse();
        message.PendingEvents.OfType<MessageRetractedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Retract_twice_throws_on_second_call()
    {
        var message = NewMessage();
        message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        var act = () => message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddReaction_on_retracted_message_throws()
    {
        var message = NewMessage();
        message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        var act = () => message.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);

        act.Should().Throw<DomainException>().WithMessage("*retracted*");
    }

    [Fact]
    public void RemoveReaction_on_retracted_message_still_works_since_it_has_no_guard()
    {
        // RemoveReaction has no EnsureNotRetracted guard; removing a nonexistent reaction throws "Reaction not found".
        var message = NewMessage();
        message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        var act = () => message.RemoveReaction(Guid.NewGuid(), Emoji.Like);

        act.Should().Throw<DomainException>().WithMessage("Reaction not found.");
    }

    // ---- Reactions ----

    [Fact]
    public void AddReaction_free_user_can_add_up_to_limit()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();

        message.AddReaction(userId, Emoji.Like, isPremium: false);

        message.Reactions.Should().ContainSingle(r => r.UserId == userId && r.Emoji == Emoji.Like);
    }

    [Fact]
    public void AddReaction_free_user_beyond_limit_throws()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: false);

        var act = () => message.AddReaction(userId, Emoji.Heart, isPremium: false);

        act.Should().Throw<DomainException>().WithMessage("*maximum number of reactions (1)*");
    }

    [Fact]
    public void AddReaction_premium_user_can_add_up_to_premium_limit()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();

        message.AddReaction(userId, Emoji.Like, isPremium: true);
        message.AddReaction(userId, Emoji.Heart, isPremium: true);

        message.Reactions.Should().HaveCount(2);
    }

    [Fact]
    public void AddReaction_premium_user_beyond_premium_limit_throws()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: true);
        message.AddReaction(userId, Emoji.Heart, isPremium: true);

        var act = () => message.AddReaction(userId, Emoji.Wow, isPremium: true);

        act.Should().Throw<DomainException>().WithMessage("*maximum number of reactions (2)*");
    }

    [Fact]
    public void AddReaction_duplicate_emoji_from_same_user_throws()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: true);

        var act = () => message.AddReaction(userId, Emoji.Like, isPremium: true);

        act.Should().Throw<DomainException>().WithMessage("*already reacted*");
    }

    [Fact]
    public void AddReaction_different_users_can_use_same_emoji()
    {
        var message = NewMessage();

        message.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);
        message.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);

        message.Reactions.Should().HaveCount(2);
    }

    [Fact]
    public void AddReaction_raises_ReactionAddedEvent()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();

        message.AddReaction(userId, Emoji.Fire, isPremium: false);

        message.PendingEvents.OfType<ReactionAddedEvent>().Should().ContainSingle()
            .Which.Emoji.Should().Be(Emoji.Fire);
    }

    [Fact]
    public void RemoveReaction_removes_existing_reaction_and_raises_event()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: false);

        message.RemoveReaction(userId, Emoji.Like);

        message.Reactions.Should().BeEmpty();
        message.PendingEvents.OfType<ReactionRemovedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RemoveReaction_nonexistent_throws()
    {
        var message = NewMessage();

        var act = () => message.RemoveReaction(Guid.NewGuid(), Emoji.Like);

        act.Should().Throw<DomainException>().WithMessage("Reaction not found.");
    }

    // ---- Broadcast read count ----

    [Fact]
    public void IncrementBroadcastReadCount_on_broadcast_message_increments()
    {
        var message = NewMessage(isBroadcast: true);

        message.IncrementBroadcastReadCount();
        message.IncrementBroadcastReadCount();

        message.BroadcastReadCount.Should().Be(2);
    }

    [Fact]
    public void IncrementBroadcastReadCount_on_non_broadcast_message_throws()
    {
        var message = NewMessage(isBroadcast: false);

        var act = () => message.IncrementBroadcastReadCount();

        act.Should().Throw<DomainException>().WithMessage("*only available for BroadcastChannel*");
    }
}
