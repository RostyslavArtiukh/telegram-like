using TelegramLike.Chats.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.ChangeMemberRole;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Application;

public class ChangeMemberRoleCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private ChangeMemberRoleCommandHandler Handler => new(_repo);

    [Fact]
    public async Task ChangeMemberRole_OwnerPromotesGroupMemberToAdmin_Persists()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        await Handler.Handle(new ChangeMemberRoleCommand(chat.Id, memberId, MemberRole.Admin, ownerId), CancellationToken.None);

        chat.FindActiveMember(memberId)!.Role.Should().Be(MemberRole.Admin);
        await _repo.Received(1).UpdateAsync(chat, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeMemberRole_ByNonOwnerInGroupChat_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        chat.Join(memberA);
        chat.Join(memberB);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(
            new ChangeMemberRoleCommand(chat.Id, memberB, MemberRole.Admin, memberA), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ChangeMemberRole_OwnerPromotesBroadcastViewerToAdmin_Succeeds()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), ChatName.Create("c"), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        await Handler.Handle(new ChangeMemberRoleCommand(chat.Id, viewerId, MemberRole.Admin, ownerId), CancellationToken.None);

        chat.FindActiveMember(viewerId)!.Role.Should().Be(MemberRole.Admin);
    }

    [Fact]
    public async Task ChangeMemberRole_ToMemberInBroadcastChannel_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), ChatName.Create("c"), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(
            new ChangeMemberRoleCommand(chat.Id, viewerId, MemberRole.Member, ownerId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Admin/Viewer*");
    }

    [Fact]
    public async Task ChangeMemberRole_InDirectChat_Throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(
            new ChangeMemberRoleCommand(chat.Id, Guid.NewGuid(), MemberRole.Admin, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*does not support role changes*");
    }
}
