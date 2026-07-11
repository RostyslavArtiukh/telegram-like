using TelegramLike.Chats.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.JoinChat;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Application;

public class JoinChatCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private JoinChatCommandHandler Handler => new(_repo);

    [Fact]
    public async Task JoinChat_WhenChatNotFound_Throws()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(new JoinChatCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task JoinChat_GroupChat_AddsUserAsMemberAndPersists()
    {
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), Guid.NewGuid());
        var userId = Guid.NewGuid();
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        await Handler.Handle(new JoinChatCommand(chat.Id, userId), CancellationToken.None);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Member);
        await _repo.Received(1).UpdateAsync(chat, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinChat_DirectChat_Throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(new JoinChatCommand(chat.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*does not support Join*");
    }
}
