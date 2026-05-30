using FluentAssertions;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Domain.Tests.Chats;

public class DirectChatTests
{
    [Fact]
    public void Create_with_same_user_throws()
    {
        var userId = Guid.NewGuid();
        var act = () => DirectChat.Create(userId, userId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_adds_both_participants_as_active_members()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var chat = DirectChat.Create(alice, bob);

        chat.Type.Should().Be(ChatType.Direct);
        chat.ActiveMembers.Should().HaveCount(2);
        chat.FindActiveMember(alice).Should().NotBeNull();
        chat.FindActiveMember(bob).Should().NotBeNull();
    }

    [Fact]
    public void Rename_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => chat.Rename(ChatName.Create("nope"), chat.Members[0].UserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => chat.Delete(chat.Members[0].UserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Leave_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => chat.Leave(chat.Members[0].UserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Kick_throws()
    {
        var chat = DirectChat.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => chat.Kick(chat.Members[1].UserId, chat.Members[0].UserId);
        act.Should().Throw<InvalidOperationException>();
    }
}
