using FluentAssertions;
using NSubstitute;
using TelegramLike.Application.Messaging.Commands.SendMessage;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Messaging.Aggregates;
using TelegramLike.Domain.Messaging.Events;
using TelegramLike.Domain.Messaging.Repositories;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Tests.Messaging;

public class SendMessageCommandHandlerTests
{
    private readonly IChatRepository _chats = Substitute.For<IChatRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();

    private SendMessageCommandHandler Handler => new(_chats, _messages);

    [Fact]
    public async Task Send_in_group_persists_message_with_MessageSent_event()
    {
        var owner = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        Message? captured = null;
        _messages.AddAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var id = await Handler.Handle(new SendMessageCommand(chat.Id, owner, "hello"), CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is MessageSentEvent);
    }

    [Fact]
    public async Task Send_throws_when_chat_not_found()
    {
        _chats.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(new SendMessageCommand(Guid.NewGuid(), Guid.NewGuid(), "hi"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Chat not found*");
    }

    [Fact]
    public async Task Send_throws_when_author_is_not_an_active_member()
    {
        var owner = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(
            new SendMessageCommand(chat.Id, Guid.NewGuid(), "hi"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not an active member*");
    }

    [Fact]
    public async Task Send_in_broadcast_by_non_admin_throws()
    {
        var owner = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var channel = BroadcastChannel.Create(ChatName.Create("News"), owner);
        channel.Join(viewer);
        _chats.GetByIdAsync(channel.Id, Arg.Any<CancellationToken>()).Returns(channel);

        var act = () => Handler.Handle(
            new SendMessageCommand(channel.Id, viewer, "scoop"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Owner or Admin*");
    }

    [Fact]
    public async Task Send_in_broadcast_initializes_read_count()
    {
        var owner = Guid.NewGuid();
        var channel = BroadcastChannel.Create(ChatName.Create("News"), owner);
        _chats.GetByIdAsync(channel.Id, Arg.Any<CancellationToken>()).Returns(channel);

        Message? captured = null;
        _messages.AddAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Handler.Handle(new SendMessageCommand(channel.Id, owner, "scoop"), CancellationToken.None);

        captured!.BroadcastReadCount.Should().Be(0);
    }

    [Fact]
    public async Task Send_reply_to_message_in_other_chat_throws()
    {
        var owner = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var foreign = Message.Send(Guid.NewGuid(), Guid.NewGuid(), MessageContent.Create("elsewhere"), []);
        _messages.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        var act = () => Handler.Handle(
            new SendMessageCommand(chat.Id, owner, "re", ReplyToMessageId: foreign.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different chat*");
    }

    [Fact]
    public async Task Send_to_deleted_chat_throws()
    {
        var owner = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        chat.Delete(owner);
        _chats.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(new SendMessageCommand(chat.Id, owner, "hi"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deleted chat*");
    }
}
