using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.ChangeMemberRole;
using TelegramLike.Chats.Application.Commands.JoinChat;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Tests;

public class JoinChatCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private JoinChatCommandHandler Handler => new(_repo);

    [Fact]
    public async Task Nonexistent_chat_throws()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(new JoinChatCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Join_a_group_chat_adds_the_user_as_Member_and_persists()
    {
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), Guid.NewGuid());
        var userId = Guid.NewGuid();
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        await Handler.Handle(new JoinChatCommand(chat.Id, userId), CancellationToken.None);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Member);
        await _repo.Received(1).UpdateAsync(chat, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Join_a_direct_chat_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(new JoinChatCommand(chat.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not support Join*");
    }
}

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

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Chat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nonexistent_chat_throws()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(new KickMemberCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}

public class ChangeMemberRoleCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private ChangeMemberRoleCommandHandler Handler => new(_repo);

    [Fact]
    public async Task Owner_promotes_a_group_member_to_admin_and_persists()
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
    public async Task Non_owner_cannot_change_roles_in_a_group_chat()
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

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Owner_promotes_a_broadcast_viewer_to_admin()
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
    public async Task Broadcast_channel_rejects_a_Member_role_change()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), ChatName.Create("c"), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(
            new ChangeMemberRoleCommand(chat.Id, viewerId, MemberRole.Member, ownerId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Admin/Viewer*");
    }

    [Fact]
    public async Task Direct_chat_rejects_role_changes()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);

        var act = () => Handler.Handle(
            new ChangeMemberRoleCommand(chat.Id, Guid.NewGuid(), MemberRole.Admin, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not support role changes*");
    }
}
