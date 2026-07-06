using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.SendMessage;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Tests;

public class SendMessageCommandHandlerTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private SendMessageCommandHandler Handler =>
        new(_messageRepository, _membership, NullLogger<SendMessageCommandHandler>.Instance);

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
    public async Task Known_chat_member_sends_and_recipients_are_derived_from_read_model()
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
        captured!.DomainEvents.OfType<TelegramLike.Messaging.Domain.Events.MessageSentEvent>()
            .Single().Recipients.Should().BeEquivalentTo([otherMember]);
    }

    [Fact]
    public async Task Known_chat_non_member_throws_unauthorized()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { Guid.NewGuid() }); // author not in the list

        var act = () => Handler.Handle(Command(chatId, authorId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _messageRepository.DidNotReceive().AddAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_chat_fails_open_and_uses_caller_supplied_recipients()
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

        captured!.DomainEvents.OfType<TelegramLike.Messaging.Domain.Events.MessageSentEvent>()
            .Single().Recipients.Should().BeEquivalentTo(callerRecipients);
    }

    [Fact]
    public async Task Reply_to_a_message_from_a_different_chat_throws()
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

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different chat*");
    }

    [Fact]
    public async Task Reply_to_a_retracted_message_throws()
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

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*retracted*");
    }

    [Fact]
    public async Task Reply_to_nonexistent_message_throws()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });
        _messageRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var command = Command(chatId, authorId) with { ReplyToMessageId = Guid.NewGuid() };

        var act = () => Handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Client_supplied_message_id_is_reused_as_idempotency_key()
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
    public async Task Empty_message_id_mints_a_new_one()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { authorId });

        var result = await Handler.Handle(Command(chatId, authorId) with { MessageId = Guid.Empty }, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
    }
}
