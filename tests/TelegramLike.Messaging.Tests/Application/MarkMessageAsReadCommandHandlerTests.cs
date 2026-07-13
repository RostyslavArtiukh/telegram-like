using TelegramLike.Messaging.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Tests.Application;

public class MarkMessageAsReadCommandHandlerTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IMessageReadReceiptRepository _receiptRepository = Substitute.For<IMessageReadReceiptRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private MarkMessageAsReadCommandHandler Handler =>
        new(_messageRepository, _receiptRepository, _membership);

    private static Message NewMessage(Guid chatId, Guid authorId, bool isBroadcast = false)
        => Message.Send(Guid.NewGuid(), chatId, authorId, MessageContent.Create("hi"), [authorId], isBroadcast: isBroadcast);

    [Fact]
    public async Task MarkAsRead_SelfRead_IsNoop()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, authorId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, authorId), CancellationToken.None);

        await _receiptRepository.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_BroadcastRead_IncrementsCountOnlyWhenReceiptIsNew()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId, isBroadcast: true);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(true);
        _receiptRepository.MarkAsReadAsync(message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId), CancellationToken.None);

        await _messageRepository.Received(1).IncrementBroadcastReadCountAsync(message.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_BroadcastRepeatRead_DoesNotIncrementAgain()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId, isBroadcast: true);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(true);
        _receiptRepository.MarkAsReadAsync(message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false); // already had a receipt

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId), CancellationToken.None);

        await _messageRepository.DidNotReceive().IncrementBroadcastReadCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_NonBroadcastRead_NeverTouchesBroadcastCount()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId, isBroadcast: false);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(true);
        _receiptRepository.MarkAsReadAsync(message.Id, readerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(message.Id, readerId), CancellationToken.None);

        await _messageRepository.DidNotReceive().IncrementBroadcastReadCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_NonexistentMessage_Throws()
    {
        _messageRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Handler.Handle(
            new MarkMessageAsReadCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task MarkAsRead_NonMemberReader_ThrowsForbidden()
    {
        // Fail-closed ([TL-101]): the read model is backfilled, so a non-member reader is
        // refused with a 403 and no receipt is written.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, readerId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(
            new MarkMessageAsReadCommand(message.Id, readerId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _receiptRepository.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}
