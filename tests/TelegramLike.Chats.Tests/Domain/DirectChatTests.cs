using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Domain;

public class DirectChatTests
{
    [Fact]
    public void Create_WithSameUserTwice_Throws()
    {
        var userId = Guid.NewGuid();
        var act = () => DirectChat.Create(Guid.NewGuid(), userId, userId);

        act.Should().Throw<DomainException>().WithMessage("*two distinct users*");
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        var act = () => DirectChat.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_AddsBothUsersAsMembers()
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
    public void Rename_Throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => chat.Rename(ChatName.Create("x"), Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*cannot be renamed*");
    }

    [Fact]
    public void Delete_Throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => chat.Delete(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*cannot be deleted*");
    }

    [Fact]
    public void Leave_Throws()
    {
        var initiatorId = Guid.NewGuid();
        var chat = DirectChat.Create(Guid.NewGuid(), initiatorId, Guid.NewGuid());

        var act = () => chat.Leave(initiatorId);

        act.Should().Throw<DomainException>().WithMessage("*does not support Leave*");
    }

    [Fact]
    public void Kick_Throws()
    {
        var initiatorId = Guid.NewGuid();
        var peerId = Guid.NewGuid();
        var chat = DirectChat.Create(Guid.NewGuid(), initiatorId, peerId);

        var act = () => chat.Kick(peerId, initiatorId);

        act.Should().Throw<DomainException>().WithMessage("*does not support Kick*");
    }
}
