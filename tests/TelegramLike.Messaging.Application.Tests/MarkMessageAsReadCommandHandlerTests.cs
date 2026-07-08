using TelegramLike.Messaging.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Tests;

public class MarkMessageAsReadCommandHandlerTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IMessageReadReceiptRepository _receiptRepository = Substitute.For<IMessageReadReceiptRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private MarkMessageAsReadCommandHandler Handler =>
        new(_messageRepository, _receiptRepository, _membership, NullLogger<MarkMessageAsReadCommandHandler>.Instance);

    private static Message NewMessage(Guid chatId, Guid authorId, bool isBroadcast = false)
        => Message.Send(Guid.NewGuid(), chatId, authorId, MessageContent.Create("hi"), [authorId], isBroadcast: isBroadcast);

    [Fact]
    public async Task Self_read_is_a_noop()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, authorId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, authorId, IsBroadcast: false), CancellationToken.None);

        await _receiptRepository.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Broadcast_read_increments_count_only_when_receipt_is_newly_created()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId, isBroadcast: true);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(true);
        _receiptRepository.MarkAsReadAsync(message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId, IsBroadcast: true), CancellationToken.None);

        await _messageRepository.Received(1).IncrementBroadcastReadCountAsync(message.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Broadcast_repeat_read_does_not_increment_count_again()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId, isBroadcast: true);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(true);
        _receiptRepository.MarkAsReadAsync(message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false); // already had a receipt

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId, IsBroadcast: true), CancellationToken.None);

        await _messageRepository.DidNotReceive().IncrementBroadcastReadCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_broadcast_read_never_touches_broadcast_count()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId, isBroadcast: false);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(true);
        _receiptRepository.MarkAsReadAsync(message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId, IsBroadcast: false), CancellationToken.None);

        await _messageRepository.DidNotReceive().IncrementBroadcastReadCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nonexistent_message_throws()
    {
        _messageRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Handler.Handle(
            new MarkMessageAsReadCommand(Guid.NewGuid(), Guid.NewGuid(), IsBroadcast: false), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Non_member_reader_still_gets_a_receipt_marked_fail_open()
    {
        // AddReaction/RemoveReaction/MarkMessageAsRead currently fail-open on membership
        // (log-only) — documenting current behavior per the messaging CLAUDE.md.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(false);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId, IsBroadcast: false), CancellationToken.None);

        await _receiptRepository.Received(1).MarkAsReadAsync(
            message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}
