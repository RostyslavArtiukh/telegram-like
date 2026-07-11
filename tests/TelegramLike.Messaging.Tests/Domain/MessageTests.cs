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
    public void Send_WithEmptyMessageId_Throws()
    {
        var act = () => Message.Send(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), MessageContent.Create("hi"), [Guid.NewGuid()]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_WithEmptyChatId_Throws()
    {
        var act = () => Message.Send(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), MessageContent.Create("hi"), [Guid.NewGuid()]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_WithEmptyAuthorId_Throws()
    {
        var act = () => Message.Send(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, MessageContent.Create("hi"), [Guid.NewGuid()]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Send_WithNullRecipients_Throws()
    {
        var act = () => Message.Send(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MessageContent.Create("hi"), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Send_RaisesMessageSentEventWithRecipients()
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
    public void Send_NonBroadcast_HasNullBroadcastReadCount()
    {
        var message = NewMessage(isBroadcast: false);

        message.BroadcastReadCount.Should().BeNull();
    }

    [Fact]
    public void Send_Broadcast_StartsBroadcastReadCountAtZero()
    {
        var message = NewMessage(isBroadcast: true);

        message.BroadcastReadCount.Should().Be(0);
    }

    [Fact]
    public void Send_WithReply_CarriesReplyReferenceInEvent()
    {
        var replyToId = Guid.NewGuid();
        var message = NewMessage(replyTo: ReplyReference.To(replyToId));

        message.ReplyTo!.ReplyToMessageId.Should().Be(replyToId);
        message.PendingEvents.OfType<MessageSentEvent>().Single().ReplyToMessageId.Should().Be(replyToId);
    }

    [Fact]
    public void Send_WithForward_CarriesForwardReferenceInEvent()
    {
        var originalMessageId = Guid.NewGuid();
        var originalChatId = Guid.NewGuid();
        var message = NewMessage(forwardFrom: ForwardReference.From(originalMessageId, originalChatId));

        message.ForwardFrom!.OriginalMessageId.Should().Be(originalMessageId);
        message.ForwardFrom!.OriginalChatId.Should().Be(originalChatId);
        message.PendingEvents.OfType<MessageSentEvent>().Single().ForwardOriginalMessageId.Should().Be(originalMessageId);
    }

    [Fact]
    public void ReplyReferenceTo_WithEmptyId_Throws()
    {
        var act = () => ReplyReference.To(Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ForwardReferenceFrom_WithEmptyMessageId_Throws()
    {
        var act = () => ForwardReference.From(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ForwardReferenceFrom_WithEmptyChatId_Throws()
    {
        var act = () => ForwardReference.From(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    // ---- Retract ----

    [Fact]
    public void Retract_ByAuthorOrModerator_MarksRetractedAndRaisesEvent()
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
    public void Retract_WithoutAuthorization_ThrowsAndDoesNotMutate()
    {
        var message = NewMessage();

        var act = () => message.Retract(Guid.NewGuid(), isAuthorOrModerator: false);

        act.Should().Throw<DomainException>();
        message.IsRetracted.Should().BeFalse();
        message.PendingEvents.OfType<MessageRetractedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Retract_Twice_ThrowsOnSecondCall()
    {
        var message = NewMessage();
        message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        var act = () => message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddReaction_OnRetractedMessage_Throws()
    {
        var message = NewMessage();
        message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        var act = () => message.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);

        act.Should().Throw<DomainException>().WithMessage("*retracted*");
    }

    [Fact]
    public void RemoveReaction_OnRetractedMessage_StillWorksSinceItHasNoGuard()
    {
        // RemoveReaction has no EnsureNotRetracted guard; removing a nonexistent reaction throws "Reaction not found".
        var message = NewMessage();
        message.Retract(Guid.NewGuid(), isAuthorOrModerator: true);

        var act = () => message.RemoveReaction(Guid.NewGuid(), Emoji.Like);

        act.Should().Throw<DomainException>().WithMessage("Reaction not found.");
    }

    // ---- Reactions ----

    [Fact]
    public void AddReaction_FreeUser_CanAddUpToLimit()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();

        message.AddReaction(userId, Emoji.Like, isPremium: false);

        message.Reactions.Should().ContainSingle(r => r.UserId == userId && r.Emoji == Emoji.Like);
    }

    [Fact]
    public void AddReaction_FreeUserBeyondLimit_Throws()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: false);

        var act = () => message.AddReaction(userId, Emoji.Heart, isPremium: false);

        act.Should().Throw<DomainException>().WithMessage("*maximum number of reactions (1)*");
    }

    [Fact]
    public void AddReaction_PremiumUser_CanAddUpToPremiumLimit()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();

        message.AddReaction(userId, Emoji.Like, isPremium: true);
        message.AddReaction(userId, Emoji.Heart, isPremium: true);

        message.Reactions.Should().HaveCount(2);
    }

    [Fact]
    public void AddReaction_PremiumUserBeyondPremiumLimit_Throws()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: true);
        message.AddReaction(userId, Emoji.Heart, isPremium: true);

        var act = () => message.AddReaction(userId, Emoji.Wow, isPremium: true);

        act.Should().Throw<DomainException>().WithMessage("*maximum number of reactions (2)*");
    }

    [Fact]
    public void AddReaction_DuplicateEmojiFromSameUser_Throws()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: true);

        var act = () => message.AddReaction(userId, Emoji.Like, isPremium: true);

        act.Should().Throw<DomainException>().WithMessage("*already reacted*");
    }

    [Fact]
    public void AddReaction_DifferentUsers_CanUseSameEmoji()
    {
        var message = NewMessage();

        message.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);
        message.AddReaction(Guid.NewGuid(), Emoji.Like, isPremium: false);

        message.Reactions.Should().HaveCount(2);
    }

    [Fact]
    public void AddReaction_RaisesReactionAddedEvent()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();

        message.AddReaction(userId, Emoji.Fire, isPremium: false);

        message.PendingEvents.OfType<ReactionAddedEvent>().Should().ContainSingle()
            .Which.Emoji.Should().Be(Emoji.Fire);
    }

    [Fact]
    public void RemoveReaction_RemovesExistingReactionAndRaisesEvent()
    {
        var message = NewMessage();
        var userId = Guid.NewGuid();
        message.AddReaction(userId, Emoji.Like, isPremium: false);

        message.RemoveReaction(userId, Emoji.Like);

        message.Reactions.Should().BeEmpty();
        message.PendingEvents.OfType<ReactionRemovedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RemoveReaction_Nonexistent_Throws()
    {
        var message = NewMessage();

        var act = () => message.RemoveReaction(Guid.NewGuid(), Emoji.Like);

        act.Should().Throw<DomainException>().WithMessage("Reaction not found.");
    }

    // ---- Broadcast read count ----

    [Fact]
    public void IncrementBroadcastReadCount_OnBroadcastMessage_Increments()
    {
        var message = NewMessage(isBroadcast: true);

        message.IncrementBroadcastReadCount();
        message.IncrementBroadcastReadCount();

        message.BroadcastReadCount.Should().Be(2);
    }

    [Fact]
    public void IncrementBroadcastReadCount_OnNonBroadcastMessage_Throws()
    {
        var message = NewMessage(isBroadcast: false);

        var act = () => message.IncrementBroadcastReadCount();

        act.Should().Throw<DomainException>().WithMessage("*only available for BroadcastChannel*");
    }
}
