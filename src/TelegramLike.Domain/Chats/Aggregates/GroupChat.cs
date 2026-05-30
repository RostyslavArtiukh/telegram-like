using TelegramLike.Domain.Chats.Entities;
using TelegramLike.Domain.Chats.Events;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Domain.Chats.Aggregates;

public sealed class GroupChat : Chat
{
    private GroupChat() { }

    private GroupChat(Guid id, ChatName name, Guid createdBy, DateTime createdAt)
        : base(id, ChatType.Group, name, createdBy, createdAt) { }

    public static GroupChat Create(ChatName name, Guid ownerUserId)
    {
        var chat = new GroupChat(Guid.NewGuid(), name, ownerUserId, DateTime.UtcNow);
        var owner = Member.Join(ownerUserId, MemberRole.Owner);
        chat._members.Add(owner);

        chat.RaiseDomainEvent(new ChatCreatedEvent(chat.Id, ChatType.Group, ownerUserId));
        chat.RaiseDomainEvent(new MemberJoinedEvent(chat.Id, ownerUserId, MemberRole.Owner, chat.RecipientsExcept(ownerUserId)));
        return chat;
    }

    public static GroupChat Reconstitute(
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
            throw new InvalidOperationException("User is banned from this chat.");
        if (existing is { Status: MemberStatus.Active })
            return;

        if (existing is not null)
            _members.Remove(existing);

        var member = Member.Join(userId, MemberRole.Member);
        _members.Add(member);
        RaiseDomainEvent(new MemberJoinedEvent(Id, userId, MemberRole.Member, RecipientsExcept(userId)));
    }

    public override void Leave(Guid userId)
    {
        EnsureNotDeleted();
        var member = RequireActiveMember(userId);
        if (member.Role == MemberRole.Owner)
            throw new InvalidOperationException("Owner must transfer ownership before leaving.");

        member.Leave();
        RaiseDomainEvent(new MemberLeftEvent(Id, userId));
    }

    public override void Kick(Guid targetUserId, Guid kickedBy)
    {
        EnsureNotDeleted();
        var actor = RequireActiveMember(kickedBy);
        var target = RequireActiveMember(targetUserId);

        if (actor.Role != MemberRole.Owner && actor.Role != MemberRole.Admin)
            throw new InvalidOperationException("Only Owner or Admin can kick.");
        if (target.Role == MemberRole.Owner)
            throw new InvalidOperationException("Cannot kick the Owner.");
        if (target.Role == MemberRole.Admin && actor.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only Owner can kick an Admin.");

        target.Kick(kickedBy);
        RaiseDomainEvent(new MemberKickedEvent(Id, targetUserId, kickedBy, RecipientsExcept(kickedBy)));
    }

    public void Ban(Guid targetUserId, Guid bannedBy, string? reason)
    {
        EnsureNotDeleted();
        var actor = RequireActiveMember(bannedBy);

        if (actor.Role != MemberRole.Owner && actor.Role != MemberRole.Admin)
            throw new InvalidOperationException("Only Owner or Admin can ban.");

        var target = FindAnyMember(targetUserId)
                     ?? throw new InvalidOperationException("Target user is not part of this chat.");

        if (target.Role == MemberRole.Owner)
            throw new InvalidOperationException("Cannot ban the Owner.");
        if (target.Role == MemberRole.Admin && actor.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only Owner can ban an Admin.");

        target.Ban(bannedBy, reason);
        RaiseDomainEvent(new MemberBannedEvent(Id, targetUserId, bannedBy, reason));
    }

    public void ChangeMemberRole(Guid targetUserId, MemberRole newRole, Guid changedBy)
    {
        EnsureNotDeleted();
        if (newRole == MemberRole.Owner)
            throw new InvalidOperationException("Use TransferOwnership to assign Owner.");
        if (newRole == MemberRole.Viewer)
            throw new InvalidOperationException("Viewer role is only valid in BroadcastChannel.");

        var actor = RequireActiveMember(changedBy);
        var target = RequireActiveMember(targetUserId);

        if (actor.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only Owner can change roles.");
        if (target.Role == MemberRole.Owner)
            throw new InvalidOperationException("Cannot change Owner's role directly.");
        if (target.Role == newRole)
            return;

        var oldRole = target.Role;
        target.ChangeRole(newRole);
        RaiseDomainEvent(new MemberRoleChangedEvent(Id, targetUserId, oldRole, newRole, changedBy));
    }

    public void TransferOwnership(Guid newOwnerUserId, Guid currentOwnerUserId)
    {
        EnsureNotDeleted();
        var currentOwner = RequireActiveMember(currentOwnerUserId);
        var newOwner = RequireActiveMember(newOwnerUserId);

        if (currentOwner.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only current Owner can transfer ownership.");
        if (newOwnerUserId == currentOwnerUserId)
            throw new InvalidOperationException("Cannot transfer ownership to yourself.");

        var previousOwnerOldRole = currentOwner.Role;
        var newOwnerOldRole = newOwner.Role;

        currentOwner.ChangeRole(MemberRole.Admin);
        newOwner.ChangeRole(MemberRole.Owner);

        RaiseDomainEvent(new MemberRoleChangedEvent(Id, currentOwnerUserId, previousOwnerOldRole, MemberRole.Admin, currentOwnerUserId));
        RaiseDomainEvent(new MemberRoleChangedEvent(Id, newOwnerUserId, newOwnerOldRole, MemberRole.Owner, currentOwnerUserId));
        RaiseDomainEvent(new OwnershipTransferredEvent(Id, currentOwnerUserId, newOwnerUserId));
    }
}
