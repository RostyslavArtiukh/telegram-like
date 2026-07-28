using TelegramLike.Chats.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.DeleteChat;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Application;

public class DeleteChatCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private readonly ChatsMetrics _metrics = new();
    private DeleteChatCommandHandler Handler => new(_repo, _metrics);

    private T Stored<T>(T chat) where T : Chat
    {
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);
        return chat;
    }

    [Fact]
    public async Task DeleteChat_ByOwner_MarksDeletedAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var chat = Stored(GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId));

        await Handler.Handle(new DeleteChatCommand(chat.Id, ownerId), CancellationToken.None);

        chat.IsDeleted.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(chat, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteChat_RaisesChatDeletedEvent()
    {
        var ownerId = Guid.NewGuid();
        var chat = Stored(GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId));

        await Handler.Handle(new DeleteChatCommand(chat.Id, ownerId), CancellationToken.None);

        // This event is what stops Messaging and Presence accepting traffic for the chat.
        var deleted = chat.PendingEvents.OfType<ChatDeletedEvent>().Should().ContainSingle().Subject;
        deleted.ChatId.Should().Be(chat.Id);
        deleted.DeletedBy.Should().Be(ownerId);
    }

    [Fact]
    public async Task DeleteChat_OnBroadcastChannel_IsAllowedForTheOwner()
    {
        var ownerId = Guid.NewGuid();
        var channel = Stored(BroadcastChannel.Create(Guid.NewGuid(), ChatName.Create("c"), ownerId));

        await Handler.Handle(new DeleteChatCommand(channel.Id, ownerId), CancellationToken.None);

        channel.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteChat_ByNonOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        chat.Join(memberId);
        Stored(chat);

        var act = () => Handler.Handle(new DeleteChatCommand(chat.Id, memberId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Only Owner*");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Chat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteChat_ByNonMember_Throws()
    {
        var chat = Stored(GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), Guid.NewGuid()));

        var act = () => Handler.Handle(new DeleteChatCommand(chat.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeleteChat_OnDirectChat_Throws()
    {
        var userA = Guid.NewGuid();
        var direct = Stored(DirectChat.Create(Guid.NewGuid(), userA, Guid.NewGuid()));

        var act = () => Handler.Handle(new DeleteChatCommand(direct.Id, userA), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*cannot be deleted*");
    }

    [Fact]
    public async Task DeleteChat_Twice_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = Stored(GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId));
        await Handler.Handle(new DeleteChatCommand(chat.Id, ownerId), CancellationToken.None);

        var act = () => Handler.Handle(new DeleteChatCommand(chat.Id, ownerId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*deleted*");
    }

    [Fact]
    public async Task DeleteChat_UnknownChat_Throws()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(new DeleteChatCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }
}
