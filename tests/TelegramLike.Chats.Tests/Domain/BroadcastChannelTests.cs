using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Domain;

public class BroadcastChannelTests
{
    private static ChatName Name(string s = "Channel") => ChatName.Create(s);

    [Fact]
    public void Create_AddsCreatorAsOwner()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        chat.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Owner);
        chat.PendingEvents.OfType<ChatCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Join_NewMember_DefaultsToViewerRole()
    {
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), Guid.NewGuid());
        var userId = Guid.NewGuid();

        chat.Join(userId);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Viewer);
        chat.PendingEvents.OfType<MemberJoinedEvent>().Should().ContainSingle(e => e.UserId == userId && e.Role == MemberRole.Viewer);
    }

    [Fact]
    public void Join_AfterBeingKicked_ReAddsAsFreshViewer()
    {
        // BroadcastChannel has no Ban (unlike GroupChat); a kicked member is revived
        // as a plain Viewer on rejoin rather than permanently blocked.
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var userId = Guid.NewGuid();
        chat.Join(userId);
        chat.Kick(userId, ownerId);

        chat.Join(userId);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Viewer);
    }

    [Fact]
    public void Join_AfterBeingKicked_RevivesTheSameMemberRow()
    {
        // Same row-reuse contract as GroupChat: a replacement row would strand the
        // kicked one in chat_members forever.
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var userId = Guid.NewGuid();
        chat.Join(userId);
        var originalRowId = chat.FindActiveMember(userId)!.Id;
        chat.Kick(userId, ownerId);

        chat.Join(userId);

        chat.Members.Where(m => m.UserId == userId).Should().ContainSingle()
            .Which.Id.Should().Be(originalRowId);
        chat.FindActiveMember(userId)!.KickedBy.Should().BeNull();
    }

    [Fact]
    public void Join_AfterDemotionAndKick_ComesBackAsViewerNotAdmin()
    {
        // Reviving the row must still reset the role — a returning ex-admin rejoins
        // with no more authority than any other viewer.
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var userId = Guid.NewGuid();
        chat.Join(userId);
        chat.PromoteToAdmin(userId, ownerId);
        chat.Kick(userId, ownerId);

        chat.Join(userId);

        chat.FindActiveMember(userId)!.Role.Should().Be(MemberRole.Viewer);
    }

    [Fact]
    public void PromoteToAdmin_ByNonOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var viewerA = Guid.NewGuid();
        var viewerB = Guid.NewGuid();
        chat.Join(viewerA);
        chat.Join(viewerB);

        var act = () => chat.PromoteToAdmin(viewerB, viewerA);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner can promote*");
    }

    [Fact]
    public void PromoteToAdmin_OnOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.PromoteToAdmin(ownerId, ownerId);

        act.Should().Throw<DomainException>().WithMessage("*already highest role*");
    }

    [Fact]
    public void PromoteToAdmin_RaisesMemberRoleChangedEvent()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        chat.ClearPendingEvents();

        chat.PromoteToAdmin(viewerId, ownerId);

        chat.FindActiveMember(viewerId)!.Role.Should().Be(MemberRole.Admin);
        chat.PendingEvents.OfType<MemberRoleChangedEvent>().Should().ContainSingle(e =>
            e.OldRole == MemberRole.Viewer && e.NewRole == MemberRole.Admin);
    }

    [Fact]
    public void DemoteToViewer_OnOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.DemoteToViewer(ownerId, ownerId);

        act.Should().Throw<DomainException>().WithMessage("*Cannot demote the Owner*");
    }

    [Fact]
    public void DemoteToViewer_ByNonOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.PromoteToAdmin(adminId, ownerId);

        var act = () => chat.DemoteToViewer(adminId, adminId);

        act.Should().Throw<DomainException>().WithMessage("*Only Owner can demote*");
    }

    [Fact]
    public void DemoteToViewer_RaisesMemberRoleChangedEvent()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var adminId = Guid.NewGuid();
        chat.Join(adminId);
        chat.PromoteToAdmin(adminId, ownerId);
        chat.ClearPendingEvents();

        chat.DemoteToViewer(adminId, ownerId);

        chat.FindActiveMember(adminId)!.Role.Should().Be(MemberRole.Viewer);
        chat.PendingEvents.OfType<MemberRoleChangedEvent>().Should().ContainSingle(e =>
            e.OldRole == MemberRole.Admin && e.NewRole == MemberRole.Viewer);
    }

    [Fact]
    public void Kick_AdminTarget_OnlyOwnerSucceeds()
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

        act.Should().Throw<DomainException>().WithMessage("*Only Owner can kick an Admin*");
    }

    [Fact]
    public void TransferOwnership_SwapsRolesAndRaisesEventsForBothMembers()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);
        var viewerId = Guid.NewGuid();
        chat.Join(viewerId);
        chat.ClearPendingEvents();

        chat.TransferOwnership(viewerId, ownerId);

        chat.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Admin);
        chat.FindActiveMember(viewerId)!.Role.Should().Be(MemberRole.Owner);
        chat.PendingEvents.OfType<MemberRoleChangedEvent>().Should().HaveCount(2);
        chat.PendingEvents.OfType<OwnershipTransferredEvent>().Should().ContainSingle(e =>
            e.PreviousOwner == ownerId && e.NewOwner == viewerId);
    }

    [Fact]
    public void Leave_ByOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), Name(), ownerId);

        var act = () => chat.Leave(ownerId);

        act.Should().Throw<DomainException>().WithMessage("*transfer ownership*");
    }
}
