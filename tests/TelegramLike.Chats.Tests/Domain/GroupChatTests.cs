using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Domain;

public class GroupChatTests
{
    private static ChatName Name(string s = "Group") => ChatName.Create(s);

    [Fact]
    public void Create_adds_creator_as_owner_and_raises_events()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);

        chat.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Owner);
        chat.PendingEvents.OfType<ChatCreatedEvent>().Should().ContainSingle();
        chat.PendingEvents.OfType<MemberJoinedEvent>().Should().ContainSingle()
            .Which.Role.Should().Be(MemberRole.Owner);
    }

    [Fact]
    public void Create_with_empty_id_throws()
    {
        var act = () => GroupChat.Create(Guid.Empty, Name(), Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Join_new_user_defaults_to_Member_role()
    {
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), Guid.NewGuid());
        var userId = Guid.NewGuid();

        chat.Join(userId);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Member);
        chat.PendingEvents.OfType<MemberJoinedEvent>().Should().ContainSingle(e => e.UserId == userId && e.Role == MemberRole.Member);
    }

    [Fact]
    public void Join_when_already_active_is_a_noop()
    {
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), Guid.NewGuid());
        var userId = Guid.NewGuid();
        chat.Join(userId);
        chat.ClearPendingEvents();

        chat.Join(userId);

        chat.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void Join_when_banned_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var userId = Guid.NewGuid();
        chat.Join(userId);
        chat.Ban(userId, ownerId, "spam");

        var act = () => chat.Join(userId);

        act.Should().Throw<DomainException>().WithMessage("*banned*");
    }

    [Fact]
    public void Leave_by_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.Leave(ownerId);

        act.Should().Throw<DomainException>().WithMessage("*transfer ownership*");
    }

    [Fact]
    public void Leave_by_regular_member_succeeds_and_raises_event()
    {
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), Guid.NewGuid());
        var userId = Guid.NewGuid();
        chat.Join(userId);

        chat.Leave(userId);

        chat.FindActiveMember(userId).Should().BeNull();
        chat.PendingEvents.OfType<MemberLeftEvent>().Should().ContainSingle(e => e.UserId == userId);
    }

    [Fact]
    public void Kick_by_regular_member_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        chat.Join(memberId);
        chat.Join(targetId);

        var act = () => chat.Kick(targetId, memberId);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner or Admin*");
    }

    [Fact]
    public void Kick_the_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.ChangeMemberRole(adminId, MemberRole.Admin, ownerId);

        var act = () => chat.Kick(ownerId, adminId);

        act.Should().Throw<DomainException>().WithMessage("*Cannot kick the Owner*");
    }

    [Fact]
    public void Admin_cannot_kick_another_admin_only_owner_can()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var adminA = Guid.NewGuid();
        var adminB = Guid.NewGuid();
        chat.Join(adminA);
        chat.Join(adminB);
        chat.ChangeMemberRole(adminA, MemberRole.Admin, ownerId);
        chat.ChangeMemberRole(adminB, MemberRole.Admin, ownerId);

        var act = () => chat.Kick(adminB, adminA);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner can kick an Admin*");
    }

    [Fact]
    public void Owner_can_kick_an_admin()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.ChangeMemberRole(adminId, MemberRole.Admin, ownerId);

        chat.Kick(adminId, ownerId);

        chat.FindActiveMember(adminId).Should().BeNull();
    }

    [Fact]
    public void ChangeMemberRole_to_Owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);

        var act = () => chat.ChangeMemberRole(memberId, MemberRole.Owner, ownerId);

        act.Should().Throw<DomainException>().WithMessage("*TransferOwnership*");
    }

    [Fact]
    public void ChangeMemberRole_to_Viewer_throws_in_group_chat()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);

        var act = () => chat.ChangeMemberRole(memberId, MemberRole.Viewer, ownerId);

        act.Should().Throw<DomainException>().WithMessage("*only valid in BroadcastChannel*");
    }

    [Fact]
    public void ChangeMemberRole_by_non_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        chat.Join(adminId);
        chat.Join(memberId);
        chat.ChangeMemberRole(adminId, MemberRole.Admin, ownerId);

        var act = () => chat.ChangeMemberRole(memberId, MemberRole.Admin, adminId);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner can change roles*");
    }

    [Fact]
    public void ChangeMemberRole_promotes_and_raises_MemberRoleChangedEvent()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);
        chat.ClearPendingEvents();

        chat.ChangeMemberRole(memberId, MemberRole.Admin, ownerId);

        chat.FindActiveMember(memberId)!.Role.Should().Be(MemberRole.Admin);
        chat.PendingEvents.OfType<MemberRoleChangedEvent>().Should().ContainSingle(e =>
            e.UserId == memberId && e.OldRole == MemberRole.Member && e.NewRole == MemberRole.Admin);
    }

    [Fact]
    public void ChangeMemberRole_to_same_role_is_a_noop()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);
        chat.ClearPendingEvents();

        chat.ChangeMemberRole(memberId, MemberRole.Member, ownerId);

        chat.PendingEvents.OfType<MemberRoleChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void TransferOwnership_by_non_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        chat.Join(memberA);
        chat.Join(memberB);

        var act = () => chat.TransferOwnership(memberB, memberA);

        act.Should().Throw<DomainException>().WithMessage("*Only current Owner*");
    }

    [Fact]
    public void TransferOwnership_to_self_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.TransferOwnership(ownerId, ownerId);

        act.Should().Throw<DomainException>().WithMessage("*yourself*");
    }

    [Fact]
    public void TransferOwnership_swaps_roles_and_raises_events_for_both_members()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);
        chat.ClearPendingEvents();

        chat.TransferOwnership(memberId, ownerId);

        chat.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Admin);
        chat.FindActiveMember(memberId)!.Role.Should().Be(MemberRole.Owner);
        chat.PendingEvents.OfType<MemberRoleChangedEvent>().Should().HaveCount(2);
        chat.PendingEvents.OfType<OwnershipTransferredEvent>().Should().ContainSingle(e =>
            e.PreviousOwner == ownerId && e.NewOwner == memberId);
    }

    [Fact]
    public void Rename_by_member_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var memberId = Guid.NewGuid();
        chat.Join(memberId);

        var act = () => chat.Rename(Name("New name"), memberId);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner or Admin*");
    }

    [Fact]
    public void Rename_by_owner_succeeds_and_raises_event()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name("Old"), ownerId);

        chat.Rename(Name("New"), ownerId);

        chat.Name!.Value.Should().Be("New");
        chat.PendingEvents.OfType<ChatRenamedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Delete_by_non_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.ChangeMemberRole(adminId, MemberRole.Admin, ownerId);

        var act = () => chat.Delete(adminId);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner*");
    }

    [Fact]
    public void Delete_by_owner_marks_deleted_and_raises_event()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);

        chat.Delete(ownerId);

        chat.IsDeleted.Should().BeTrue();
        chat.PendingEvents.OfType<ChatDeletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Operations_on_a_deleted_chat_throw()
    {
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), Name(), ownerId);
        chat.Delete(ownerId);

        var act = () => chat.Join(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*deleted*");
    }
}
