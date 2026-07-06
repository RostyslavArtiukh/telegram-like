using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Domain.Tests;

public class BroadcastChannelTests
{
    private static ChatName Name(string s = "Channel") => ChatName.Create(s);

    [Fact]
    public void Create_adds_creator_as_owner()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        chat.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Owner);
        chat.DomainEvents.OfType<ChatCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Join_defaults_new_member_to_Viewer_role()
    {
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), Guid.NewGuid());
        var userId = Guid.NewGuid();

        chat.Join(userId);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Viewer);
        chat.DomainEvents.OfType<MemberJoinedEvent>().Should().ContainSingle(e => e.UserId == userId && e.Role == MemberRole.Viewer);
    }

    [Fact]
    public void Join_after_being_kicked_re_adds_as_a_fresh_Viewer()
    {
        // BroadcastChannel has no Ban (unlike GroupChat); a kicked member's row is
        // replaced on rejoin rather than permanently blocked.
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var userId = Guid.NewGuid();
        chat.Join(userId);
        chat.Kick(userId, ownerId);

        chat.Join(userId);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Viewer);
    }

    [Fact]
    public void PromoteToAdmin_by_non_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var viewerA = Guid.NewGuid();
        var viewerB = Guid.NewGuid();
        chat.Join(viewerA);
        chat.Join(viewerB);

        var act = () => chat.PromoteToAdmin(viewerB, viewerA);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only Owner can promote*");
    }

    [Fact]
    public void PromoteToAdmin_the_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.PromoteToAdmin(ownerId, ownerId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already highest role*");
    }

    [Fact]
    public void PromoteToAdmin_raises_MemberRoleChangedEvent()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        chat.ClearDomainEvents();

        chat.PromoteToAdmin(viewerId, ownerId);

        chat.FindActiveMember(viewerId)!.Role.Should().Be(MemberRole.Admin);
        chat.DomainEvents.OfType<MemberRoleChangedEvent>().Should().ContainSingle(e =>
            e.OldRole == MemberRole.Viewer && e.NewRole == MemberRole.Admin);
    }

    [Fact]
    public void DemoteToViewer_the_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.DemoteToViewer(ownerId, ownerId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cannot demote the Owner*");
    }

    [Fact]
    public void DemoteToViewer_by_non_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.PromoteToAdmin(adminId, ownerId);

        var act = () => chat.DemoteToViewer(adminId, adminId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only Owner can demote*");
    }

    [Fact]
    public void DemoteToViewer_raises_MemberRoleChangedEvent()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.PromoteToAdmin(adminId, ownerId);
        chat.ClearDomainEvents();

        chat.DemoteToViewer(adminId, ownerId);

        chat.FindActiveMember(adminId)!.Role.Should().Be(MemberRole.Viewer);
        chat.DomainEvents.OfType<MemberRoleChangedEvent>().Should().ContainSingle(e =>
            e.OldRole == MemberRole.Admin && e.NewRole == MemberRole.Viewer);
    }

    [Fact]
    public void Kick_hierarchy_matches_group_chat_only_owner_can_kick_admin()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var adminA = Guid.NewGuid();
        var adminB = Guid.NewGuid();
        chat.Join(adminA);
        chat.Join(adminB);
        chat.PromoteToAdmin(adminA, ownerId);
        chat.PromoteToAdmin(adminB, ownerId);

        var act = () => chat.Kick(adminB, adminA);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only Owner can kick an Admin*");
    }

    [Fact]
    public void TransferOwnership_swaps_roles_and_raises_events_for_both_members()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        chat.ClearDomainEvents();

        chat.TransferOwnership(viewerId, ownerId);

        chat.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Admin);
        chat.FindActiveMember(viewerId)!.Role.Should().Be(MemberRole.Owner);
        chat.DomainEvents.OfType<MemberRoleChangedEvent>().Should().HaveCount(2);
        chat.DomainEvents.OfType<OwnershipTransferredEvent>().Should().ContainSingle(e =>
            e.PreviousOwner == ownerId && e.NewOwner == viewerId);
    }

    [Fact]
    public void Leave_by_owner_throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.Leave(ownerId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*transfer ownership*");
    }
}
