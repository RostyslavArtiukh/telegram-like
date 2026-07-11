using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Domain;

public class DirectChatTests
{
    [Fact]
    public void Create_with_same_user_twice_throws()
    {
        var userId = Guid.NewGuid();
        var act = () => DirectChat.Create(Guid.NewGuid(), userId, userId);

        act.Should().Throw<DomainException>().WithMessage("*two distinct users*");
    }

    [Fact]
    public void Create_with_empty_id_throws()
    {
        var act = () => DirectChat.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_adds_both_users_as_Member_role()
    {
        var initiatorId = Guid.NewGuid();
        var peerId = Guid.NewGuid();

        var chat = DirectChat.Create(Guid.NewGuid(), initiatorId, peerId);

        chat.FindActiveMember(initiatorId)!.Role.Should().Be(MemberRole.Member);
        chat.FindActiveMember(peerId)!.Role.Should().Be(MemberRole.Member);
        chat.PendingEvents.OfType<MemberJoinedEvent>().Should().HaveCount(2);
        chat.PendingEvents.OfType<ChatCreatedEvent>().Should().ContainSingle(e => e.Type == ChatType.Direct);
    }

    [Fact]
    public void Rename_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => chat.Rename(ChatName.Create("x"), Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*cannot be renamed*");
    }

    [Fact]
    public void Delete_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => chat.Delete(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*cannot be deleted*");
    }

    [Fact]
    public void Leave_throws()
    {
        var initiatorId = Guid.NewGuid();
        var chat = DirectChat.Create(Guid.NewGuid(), initiatorId, Guid.NewGuid());

        var act = () => chat.Leave(initiatorId);

        act.Should().Throw<DomainException>().WithMessage("*does not support Leave*");
    }

    [Fact]
    public void Kick_throws()
    {
        var initiatorId = Guid.NewGuid();
        var peerId = Guid.NewGuid();
        var chat = DirectChat.Create(Guid.NewGuid(), initiatorId, peerId);

        var act = () => chat.Kick(peerId, initiatorId);

        act.Should().Throw<DomainException>().WithMessage("*does not support Kick*");
    }
}
