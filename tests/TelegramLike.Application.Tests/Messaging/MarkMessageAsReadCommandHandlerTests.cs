using FluentAssertions;
using NSubstitute;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Application.Messaging.Commands.MarkMessageAsRead;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Messaging.Aggregates;
using TelegramLike.Domain.Messaging.Repositories;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Tests.Messaging;

public class MarkMessageAsReadCommandHandlerTests
{
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IChatRepository _chats = Substitute.For<IChatRepository>();
    private readonly IMessageReadReceiptRepository _receipts = Substitute.For<IMessageReadReceiptRepository>();

    private MarkMessageAsReadCommandHandler Handler => new(_messages, _chats, _receipts);

    [Fact]
    public async Task Self_read_is_skipped()
    {
        var author = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), author);
        var msg = Message.Send(chat.Id, author, MessageContent.Create("hi"), []);

        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        await Handler.Handle(new MarkMessageAsReadCommand(msg.Id, author), CancellationToken.None);

        await _receipts.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Group_reader_creates_receipt()
    {
        var author = Guid.NewGuid();
        var reader = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), author);
        chat.Join(reader);
        var msg = Message.Send(chat.Id, author, MessageContent.Create("hi"), []);

        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);
        _receipts.HasReceiptAsync(msg.Id, reader, Arg.Any<CancellationToken>()).Returns(false);

        await Handler.Handle(new MarkMessageAsReadCommand(msg.Id, reader), CancellationToken.None);

        await _receipts.Received(1).MarkAsReadAsync(
            msg.Id, reader, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_receipt_is_skipped()
    {
        var author = Guid.NewGuid();
        var reader = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), author);
        chat.Join(reader);
        var msg = Message.Send(chat.Id, author, MessageContent.Create("hi"), []);

        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);
        _receipts.HasReceiptAsync(msg.Id, reader, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new MarkMessageAsReadCommand(msg.Id, reader), CancellationToken.None);

        await _receipts.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Broadcast_increments_read_count_on_message_aggregate()
    {
        var owner = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var channel = BroadcastChannel.Create(ChatName.Create("News"), owner);
        channel.Join(viewer);
        var msg = Message.Send(channel.Id, owner, MessageContent.Create("scoop"), [], isBroadcast: true);

        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _chats.GetByIdAsync(channel.Id, Arg.Any<CancellationToken>()).Returns(channel);

        await Handler.Handle(new MarkMessageAsReadCommand(msg.Id, viewer), CancellationToken.None);

        msg.BroadcastReadCount.Should().Be(1);
        await _messages.Received(1).UpdateAsync(msg, Arg.Any<CancellationToken>());
        await _receipts.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_throws()
    {
        var author = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), author);
        var msg = Message.Send(chat.Id, author, MessageContent.Create("hi"), []);

        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(new MarkMessageAsReadCommand(msg.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active chat members*");
    }
}
