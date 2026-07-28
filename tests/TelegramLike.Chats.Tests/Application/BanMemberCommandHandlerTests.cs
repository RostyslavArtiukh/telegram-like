using TelegramLike.Chats.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.BanMember;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Application;

public class BanMemberCommandHandlerTests
{
    private readonly IChatRepository _repo = Substitute.For<IChatRepository>();
    private readonly ChatsMetrics _metrics = new();
    private BanMemberCommandHandler Handler => new(_repo, _metrics);

    private GroupChat GroupWith(Guid ownerId, params Guid[] memberIds)
    {
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        foreach (var id in memberIds) chat.Join(id);
        _repo.GetByIdAsync(chat.Id, Arg.Any<CancellationToken>()).Returns(chat);
        return chat;
    }

    [Fact]
    public async Task BanMember_ByOwner_BansAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var chat = GroupWith(ownerId, memberId);

        await Handler.Handle(new BanMemberCommand(chat.Id, memberId, ownerId, "spam"), CancellationToken.None);

        chat.FindActiveMember(memberId).Should().BeNull();
        chat.FindAnyMember(memberId)!.Status.Should().Be(MemberStatus.Banned);
        await _repo.Received(1).UpdateAsync(chat, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BanMember_RaisesMemberBannedEventCarryingTheReason()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var chat = GroupWith(ownerId, memberId);

        await Handler.Handle(new BanMemberCommand(chat.Id, memberId, ownerId, "spam"), CancellationToken.None);

        // The event is what carries the ban to Messaging/Presence — without it the ban
        // would only block rejoining while the user kept posting.
        var banned = chat.PendingEvents.OfType<MemberBannedEvent>().Should().ContainSingle().Subject;
        banned.UserId.Should().Be(memberId);
        banned.BannedBy.Should().Be(ownerId);
        banned.Reason.Should().Be("spam");
    }

    [Fact]
    public async Task BanMember_BlocksTheUserFromRejoining()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var chat = GroupWith(ownerId, memberId);

        await Handler.Handle(new BanMemberCommand(chat.Id, memberId, ownerId, null), CancellationToken.None);

        var rejoin = () => chat.Join(memberId);
        rejoin.Should().Throw<DomainException>().WithMessage("*banned*");
    }

    [Fact]
    public async Task BanMember_ByRegularMember_Throws()
    {
        var ownerId = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var chat = GroupWith(ownerId, memberA, memberB);

        var act = () => Handler.Handle(new BanMemberCommand(chat.Id, memberB, memberA, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Chat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BanMember_OnBroadcastChannel_Throws()
    {
        // Broadcast has no ban — a viewer is kicked and may come back.
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var channel = BroadcastChannel.Create(Guid.NewGuid(), ChatName.Create("c"), ownerId);
        channel.Join(viewerId);
        _repo.GetByIdAsync(channel.Id, Arg.Any<CancellationToken>()).Returns(channel);

        var act = () => Handler.Handle(new BanMemberCommand(channel.Id, viewerId, ownerId, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*does not support banning*");
    }

    [Fact]
    public async Task BanMember_OnDirectChat_Throws()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var direct = DirectChat.Create(Guid.NewGuid(), userA, userB);
        _repo.GetByIdAsync(direct.Id, Arg.Any<CancellationToken>()).Returns(direct);

        var act = () => Handler.Handle(new BanMemberCommand(direct.Id, userB, userA, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*does not support banning*");
    }

    [Fact]
    public async Task BanMember_UnknownChat_Throws()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Chat?)null);

        var act = () => Handler.Handle(
            new BanMemberCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }
}
