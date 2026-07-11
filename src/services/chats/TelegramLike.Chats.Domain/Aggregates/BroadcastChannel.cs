using TelegramLike.Chats.Domain.Entities;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Domain.Aggregates;

public sealed class BroadcastChannel : Chat
{
    private BroadcastChannel() { }

    private BroadcastChannel(Guid id, ChatName name, Guid createdBy, DateTime createdAt)
        : base(id, ChatType.Broadcast, name, createdBy, createdAt) { }

    public static BroadcastChannel Create(Guid id, ChatName name, Guid ownerUserId)
    {
        // Caller-supplied id doubles as the duplicate-protection key (see ChatRepository.AddAsync).
        if (id == Guid.Empty) throw new DomainException("Chat id cannot be empty.");
        var chat = new BroadcastChannel(id, name, ownerUserId, DateTime.UtcNow);
        var owner = Member.Join(ownerUserId, MemberRole.Owner);
        chat._members.Add(owner);

        chat.RecordEvent(new ChatCreatedEvent(chat.Id, ChatType.Broadcast, ownerUserId));
        chat.RecordEvent(new MemberJoinedEvent(chat.Id, ownerUserId, MemberRole.Owner, chat.RecipientsExcept(ownerUserId)));
        return chat;
    }

    public static BroadcastChannel FromStorage(
        Guid id, ChatName name, Guid createdBy, DateTime createdAt, DateTime? deletedAt, IEnumerable<Member> members)
    {
        var chat = new BroadcastChannel(id, name, createdBy, createdAt) { DeletedAt = deletedAt };
        chat._members.AddRange(members);
        return chat;
    }

    public void Join(Guid userId)
    {
        EnsureNotDeleted();

        var existing = FindAnyMember(userId);
        if (existing is { Status: MemberStatus.Banned })
            throw new DomainException("User is banned from this channel.");
        if (existing is { Status: MemberStatus.Active })
            return;

        if (existing is not null)
            _members.Remove(existing);

        var member = Member.Join(userId, MemberRole.Viewer);
        _members.Add(member);
        RecordEvent(new MemberJoinedEvent(Id, userId, MemberRole.Viewer, RecipientsExcept(userId)));
    }

    public override void Leave(Guid userId)
    {
        EnsureNotDeleted();
        var member = RequireActiveMember(userId);
        if (member.Role == MemberRole.Owner)
            throw new DomainException("Owner must transfer ownership before leaving.");

        member.Leave();
        RecordEvent(new MemberLeftEvent(Id, userId));
    }

    public override void Kick(Guid memberUserId, Guid kickedBy)
    {
        EnsureNotDeleted();
        var actingMember = RequireActiveMember(kickedBy);
        var affectedMember = RequireActiveMember(memberUserId);

        if (actingMember.Role != MemberRole.Owner && actingMember.Role != MemberRole.Admin)
            throw new DomainException("Only Owner or Admin can kick.");
        if (affectedMember.Role == MemberRole.Owner)
            throw new DomainException("Cannot kick the Owner.");
        if (affectedMember.Role == MemberRole.Admin && actingMember.Role != MemberRole.Owner)
            throw new DomainException("Only Owner can kick an Admin.");

        affectedMember.Kick(kickedBy);
        RecordEvent(new MemberKickedEvent(Id, memberUserId, kickedBy, RecipientsExcept(kickedBy)));
    }

    public void PromoteToAdmin(Guid memberUserId, Guid promotedBy)
    {
        EnsureNotDeleted();
        var actingMember = RequireActiveMember(promotedBy);
        var affectedMember = RequireActiveMember(memberUserId);

        if (actingMember.Role != MemberRole.Owner)
            throw new DomainException("Only Owner can promote to Admin.");
        if (affectedMember.Role == MemberRole.Owner)
            throw new DomainException("Owner is already highest role.");
        if (affectedMember.Role == MemberRole.Admin)
            return;

        var oldRole = affectedMember.Role;
        affectedMember.ChangeRole(MemberRole.Admin);
        RecordEvent(new MemberRoleChangedEvent(Id, memberUserId, oldRole, MemberRole.Admin, promotedBy));
    }

    public void DemoteToViewer(Guid memberUserId, Guid demotedBy)
    {
        EnsureNotDeleted();
        var actingMember = RequireActiveMember(demotedBy);
        var affectedMember = RequireActiveMember(memberUserId);

        if (actingMember.Role != MemberRole.Owner)
            throw new DomainException("Only Owner can demote.");
        if (affectedMember.Role == MemberRole.Owner)
            throw new DomainException("Cannot demote the Owner.");
        if (affectedMember.Role == MemberRole.Viewer)
            return;

        var oldRole = affectedMember.Role;
        affectedMember.ChangeRole(MemberRole.Viewer);
        RecordEvent(new MemberRoleChangedEvent(Id, memberUserId, oldRole, MemberRole.Viewer, demotedBy));
    }

    public void TransferOwnership(Guid newOwnerUserId, Guid currentOwnerUserId)
    {
        EnsureNotDeleted();
        var currentOwner = RequireActiveMember(currentOwnerUserId);
        var newOwner = RequireActiveMember(newOwnerUserId);

        if (currentOwner.Role != MemberRole.Owner)
            throw new DomainException("Only current Owner can transfer ownership.");
        if (newOwnerUserId == currentOwnerUserId)
            throw new DomainException("Cannot transfer ownership to yourself.");

        var previousOwnerOldRole = currentOwner.Role;
        var newOwnerOldRole = newOwner.Role;

        currentOwner.ChangeRole(MemberRole.Admin);
        newOwner.ChangeRole(MemberRole.Owner);

        RecordEvent(new MemberRoleChangedEvent(Id, currentOwnerUserId, previousOwnerOldRole, MemberRole.Admin, currentOwnerUserId));
        RecordEvent(new MemberRoleChangedEvent(Id, newOwnerUserId, newOwnerOldRole, MemberRole.Owner, currentOwnerUserId));
        RecordEvent(new OwnershipTransferredEvent(Id, currentOwnerUserId, newOwnerUserId));
    }
}
