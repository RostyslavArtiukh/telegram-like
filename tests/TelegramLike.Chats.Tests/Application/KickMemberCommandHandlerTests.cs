using TelegramLike.Chats.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Application;

public class KickMemberCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private KickMemberCommandHandler Handler => new(_repo);

    [Fact]
    public async Task Owner_kicks_a_member_and_persists()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        await Handler.Handle(new KickMemberCommand(chat.Id, memberId, ownerId), CancellationToken.None);

        chat.FindActiveMember(memberId).Should().BeNull();
        await _repo.Received(1).UpdateAsync(chat, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Regular_member_cannot_kick()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        chat.Join(memberA);
        chat.Join(memberB);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(new KickMemberCommand(chat.Id, memberB, memberA), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Chat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nonexistent_chat_throws()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(new KickMemberCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }
}
