using TelegramLike.Chats.Domain.Entities;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Domain.Aggregates;

public sealed class GroupChat : Chat
{
    private GroupChat() { }

    private GroupChat(Guid id, ChatName name, Guid createdBy, DateTime createdAt)
        : base(id, ChatType.Group, name, createdBy, createdAt) { }

    public static GroupChat Create(Guid id, ChatName name, Guid ownerUserId)
    {
        // Caller-supplied id doubles as the duplicate-protection key (see ChatRepository.AddAsync).
        if (id == Guid.Empty) throw new DomainException("Chat id cannot be empty.");
        var chat = new GroupChat(id, name, ownerUserId, DateTime.UtcNow);
        var owner = Member.Join(ownerUserId, MemberRole.Owner);
        chat._members.Add(owner);

        chat.RecordEvent(new ChatCreatedEvent(chat.Id, ChatType.Group, ownerUserId));
        chat.RecordEvent(new MemberJoinedEvent(chat.Id, ownerUserId, MemberRole.Owner, chat.RecipientsExcept(ownerUserId)));
        return chat;
    }

    public static GroupChat FromStorage(
        Guid id, ChatName name, Guid createdBy, DateTime createdAt, DateTime? deletedAt, IEnumerable<Member> members)
    {
        var chat = new GroupChat(id, name, createdBy, createdAt) { DeletedAt = deletedAt };
        chat._members.AddRange(members);
        return chat;
    }

    public void Join(Guid userId)
    {
        EnsureNotDeleted();

        var existing = FindAnyMember(userId);
        if (existing is { Status: MemberStatus.Banned })
            throw new DomainException("User is banned from this chat.");
        if (existing is { Status: MemberStatus.Active })
            return;

        // Revive the existing row instead of replacing it — see Member.Rejoin.
        if (existing is not null)
            existing.Rejoin(MemberRole.Member);
        else
            _members.Add(Member.Join(userId, MemberRole.Member));

        RecordEvent(new MemberJoinedEvent(Id, userId, MemberRole.Member, RecipientsExcept(userId)));
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

    public void Ban(Guid memberUserId, Guid bannedBy, string? reason)
    {
        EnsureNotDeleted();
        var actingMember = RequireActiveMember(bannedBy);

        if (actingMember.Role != MemberRole.Owner && actingMember.Role != MemberRole.Admin)
            throw new DomainException("Only Owner or Admin can ban.");

        var affectedMember = FindAnyMember(memberUserId)
                     ?? throw new DomainException("Target user is not part of this chat.");

        if (affectedMember.Role == MemberRole.Owner)
            throw new DomainException("Cannot ban the Owner.");
        if (affectedMember.Role == MemberRole.Admin && actingMember.Role != MemberRole.Owner)
            throw new DomainException("Only Owner can ban an Admin.");

        affectedMember.Ban(bannedBy, reason);
        RecordEvent(new MemberBannedEvent(Id, memberUserId, bannedBy, reason));
    }

    public void ChangeMemberRole(Guid memberUserId, MemberRole newRole, Guid changedBy)
    {
        EnsureNotDeleted();
        if (newRole == MemberRole.Owner)
            throw new DomainException("Use TransferOwnership to assign Owner.");
        if (newRole == MemberRole.Viewer)
            throw new DomainException("Viewer role is only valid in BroadcastChannel.");

        var actingMember = RequireActiveMember(changedBy);
        var affectedMember = RequireActiveMember(memberUserId);

        if (actingMember.Role != MemberRole.Owner)
            throw new DomainException("Only Owner can change roles.");
        if (affectedMember.Role == MemberRole.Owner)
            throw new DomainException("Cannot change Owner's role directly.");
        if (affectedMember.Role == newRole)
            return;

        var oldRole = affectedMember.Role;
        affectedMember.ChangeRole(newRole);
        RecordEvent(new MemberRoleChangedEvent(Id, memberUserId, oldRole, newRole, changedBy));
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
