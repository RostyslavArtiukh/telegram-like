using FluentAssertions;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Events;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Domain.Tests.Chats;

public class GroupChatTests
{
    private static ChatName Name => ChatName.Create("Squad");

    [Fact]
    public void Create_adds_owner_as_active_member()
    {
        var ownerId = Guid.NewGuid();

        var chat = GroupChat.Create(Name, ownerId);

        chat.Type.Should().Be(ChatType.Group);
        chat.ActiveMembers.Should().ContainSingle()
            .Which.Role.Should().Be(MemberRole.Owner);
        chat.DomainEvents.OfType<ChatCreatedEvent>().Should().ContainSingle();
        chat.DomainEvents.OfType<MemberJoinedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Join_adds_member_with_Member_role()
    {
        var chat = GroupChat.Create(Name, Guid.NewGuid());
        var newUser = Guid.NewGuid();

        chat.Join(newUser);

        chat.FindActiveMember(newUser)!.Role.Should().Be(MemberRole.Member);
    }

    [Fact]
    public void Join_is_idempotent_for_active_member()
    {
        var chat = GroupChat.Create(Name, Guid.NewGuid());
        var newUser = Guid.NewGuid();
        chat.Join(newUser);
        var countBefore = chat.Members.Count;

        chat.Join(newUser);

        chat.Members.Count.Should().Be(countBefore);
    }

    [Fact]
    public void Join_throws_when_user_is_banned()
    {
        var owner = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        var target = Guid.NewGuid();
        chat.Join(target);
        chat.Ban(target, owner, reason: "spam");

        var act = () => chat.Join(target);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Leave_by_owner_throws()
    {
        var owner = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);

        var act = () => chat.Leave(owner);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Kick_by_non_owner_admin_throws()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        chat.Join(member);
        chat.Join(stranger);

        var act = () => chat.Kick(member, stranger);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Kick_admin_by_non_owner_throws()
    {
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var anotherAdmin = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        chat.Join(admin);
        chat.Join(anotherAdmin);
        chat.ChangeMemberRole(admin, MemberRole.Admin, owner);
        chat.ChangeMemberRole(anotherAdmin, MemberRole.Admin, owner);

        var act = () => chat.Kick(admin, anotherAdmin);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TransferOwnership_swaps_roles()
    {
        var owner = Guid.NewGuid();
        var target = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        chat.Join(target);

        chat.TransferOwnership(target, owner);

        chat.FindActiveMember(target)!.Role.Should().Be(MemberRole.Owner);
        chat.FindActiveMember(owner)!.Role.Should().Be(MemberRole.Admin);
    }

    [Fact]
    public void ChangeMemberRole_to_Owner_throws()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        chat.Join(member);

        var act = () => chat.ChangeMemberRole(member, MemberRole.Owner, owner);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ChangeMemberRole_to_Viewer_throws()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        chat.Join(member);

        var act = () => chat.ChangeMemberRole(member, MemberRole.Viewer, owner);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_by_non_owner_throws()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var chat = GroupChat.Create(Name, owner);
        chat.Join(member);

        var act = () => chat.Delete(member);

        act.Should().Throw<InvalidOperationException>();
    }
}
