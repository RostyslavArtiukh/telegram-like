using TelegramLike.Messaging.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.SendMessage;
using TelegramLike.Messaging.Application.Observability;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Tests.Application;

public class SendMessageCommandHandlerTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();
    private readonly IChatTypeReadModel _chatType = Substitute.For<IChatTypeReadModel>();
    private readonly MessagingMetrics _metrics = new();

    private SendMessageCommandHandler Handler =>
        new(_messageRepository, _membership, _chatType, _metrics, NullLogger<SendMessageCommandHandler>.Instance);

    private static SendMessageCommand Command(
        Guid chatId, Guid authorId, IReadOnlyList<Guid>? recipients = null, bool isBroadcast = false)
        => new(
            Guid.NewGuid(),
            chatId,
            authorId,
            "hello",
            recipients ?? [],
            isBroadcast);

    [Fact]
    public async Task Send_KnownChatMember_DerivesRecipientsFromReadModel()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId, otherMember });

        Message? captured = null;
        _messageRepository.AddAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Caller tries to spoof recipients with an id that isn't actually a member.
        var spoofedRecipient = Guid.NewGuid();
        await Handler.Handle(Command(chatId, authorId, [spoofedRecipient]), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.PendingEvents.OfType<TelegramLike.Messaging.Domain.Events.MessageSentEvent>()
            .Single().Recipients.Should().BeEquivalentTo([otherMember]);
    }

    [Fact]
    public async Task Send_KnownBroadcastChat_DerivesBroadcastFromReadModel_IgnoringClientFlag()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });
        _chatType.IsBroadcastAsync(chatId, Arg.Any<CancellationToken>()).Returns(true);

        Message? captured = null;
        _messageRepository.AddAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Client lies that it's not a broadcast; the read-model is authoritative.
        await Handler.Handle(Command(chatId, authorId, isBroadcast: false), CancellationToken.None);

        captured!.IsBroadcast.Should().BeTrue();
    }

    [Fact]
    public async Task Send_UnknownChatType_FallsBackToClientBroadcastFlag()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });
        _chatType.IsBroadcastAsync(chatId, Arg.Any<CancellationToken>()).Returns((bool?)null); // not materialized

        Message? captured = null;
        _messageRepository.AddAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Handler.Handle(Command(chatId, authorId, isBroadcast: true), CancellationToken.None);

        captured!.IsBroadcast.Should().BeTrue("no chat-type materialized yet → fall back to the caller flag");
    }

    [Fact]
    public async Task Send_KnownChatNonMember_ThrowsUnauthorized()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { Guid.NewGuid() }); // author not in the list

        var act = () => Handler.Handle(Command(chatId, authorId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _messageRepository.DidNotReceive().AddAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_UnknownChat_FailsOpenWithCallerSuppliedRecipients()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>()); // not materialized yet

        var callerRecipients = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        Message? captured = null;
        _messageRepository.AddAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Handler.Handle(Command(chatId, authorId, callerRecipients), CancellationToken.None);

        captured!.PendingEvents.OfType<TelegramLike.Messaging.Domain.Events.MessageSentEvent>()
            .Single().Recipients.Should().BeEquivalentTo(callerRecipients);
    }

    [Fact]
    public async Task Send_DeletedChat_FailsClosedEvenThoughNoMemberIsActive()
    {
        // A deleted chat (or one whose members were all banned) is materialized here with every
        // row deactivated. Reading "no active members" as "unknown chat" would put it straight
        // into the fail-open branch above and let anyone keep posting into it.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _membership.IsChatKnownAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(true); // rows exist, all inactive

        var act = () => Handler.Handle(
            Command(chatId, authorId, [Guid.NewGuid()]), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _messageRepository.DidNotReceive().AddAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_ReplyToMessageFromDifferentChat_Throws()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });

        var replyTarget = Message.Send(
            Guid.NewGuid(), Guid.NewGuid() /* different chat */, Guid.NewGuid(),
            TelegramLike.Messaging.Domain.ValueObjects.MessageContent.Create("hi"), [authorId]);
        _messageRepository.GetByIdAsync(replyTarget.Id, Arg.Any<CancellationToken>()).Returns(replyTarget);

        var command = Command(chatId, authorId) with { ReplyToMessageId = replyTarget.Id };

        var act = () => Handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*different chat*");
    }

    [Fact]
    public async Task Send_ReplyToRetractedMessage_Throws()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });

        var replyTarget = Message.Send(
            Guid.NewGuid(), chatId, Guid.NewGuid(),
            TelegramLike.Messaging.Domain.ValueObjects.MessageContent.Create("hi"), [authorId]);
        replyTarget.Retract(Guid.NewGuid(), isAuthorOrModerator: true);
        _messageRepository.GetByIdAsync(replyTarget.Id, Arg.Any<CancellationToken>()).Returns(replyTarget);

        var command = Command(chatId, authorId) with { ReplyToMessageId = replyTarget.Id };

        var act = () => Handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*retracted*");
    }

    [Fact]
    public async Task Send_ReplyToNonexistentMessage_Throws()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });
        _messageRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var command = Command(chatId, authorId) with { ReplyToMessageId = Guid.NewGuid() };

        var act = () => Handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Send_ClientSuppliedMessageId_IsReusedAsDuplicateProtectionKey()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });

        var result = await Handler.Handle(Command(chatId, authorId) with { MessageId = messageId }, CancellationToken.None);

        result.Should().Be(messageId);
    }

    [Fact]
    public async Task Send_EmptyMessageId_MintsNewOne()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });

        var result = await Handler.Handle(Command(chatId, authorId) with { MessageId = Guid.Empty }, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
    }
}
