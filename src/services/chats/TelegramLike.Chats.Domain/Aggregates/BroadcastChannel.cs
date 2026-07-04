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
        // Caller-supplied id doubles as the idempotency key (see ChatRepository.AddAsync).
        if (id == Guid.Empty) throw new ArgumentException("Chat id cannot be empty.", nameof(id));
        var chat = new BroadcastChannel(id, name, ownerUserId, DateTime.UtcNow);
        var owner = Member.Join(ownerUserId, MemberRole.Owner);
        chat._members.Add(owner);

        chat.RaiseDomainEvent(new ChatCreatedEvent(chat.Id, ChatType.Broadcast, ownerUserId));
        chat.RaiseDomainEvent(new MemberJoinedEvent(chat.Id, ownerUserId, MemberRole.Owner, chat.RecipientsExcept(ownerUserId)));
        return chat;
    }

    public static BroadcastChannel Reconstitute(
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
            throw new InvalidOperationException("User is banned from this channel.");
        if (existing is { Status: MemberStatus.Active })
            return;

        if (existing is not null)
            _members.Remove(existing);

        var member = Member.Join(userId, MemberRole.Viewer);
        _members.Add(member);
        RaiseDomainEvent(new MemberJoinedEvent(Id, userId, MemberRole.Viewer, RecipientsExcept(userId)));
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

    public void PromoteToAdmin(Guid targetUserId, Guid promotedBy)
    {
        EnsureNotDeleted();
        var actor = RequireActiveMember(promotedBy);
        var target = RequireActiveMember(targetUserId);

        if (actor.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only Owner can promote to Admin.");
        if (target.Role == MemberRole.Owner)
            throw new InvalidOperationException("Owner is already highest role.");
        if (target.Role == MemberRole.Admin)
            return;

        var oldRole = target.Role;
        target.ChangeRole(MemberRole.Admin);
        RaiseDomainEvent(new MemberRoleChangedEvent(Id, targetUserId, oldRole, MemberRole.Admin, promotedBy));
    }

    public void DemoteToViewer(Guid targetUserId, Guid demotedBy)
    {
        EnsureNotDeleted();
        var actor = RequireActiveMember(demotedBy);
        var target = RequireActiveMember(targetUserId);

        if (actor.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only Owner can demote.");
        if (target.Role == MemberRole.Owner)
            throw new InvalidOperationException("Cannot demote the Owner.");
        if (target.Role == MemberRole.Viewer)
            return;

        var oldRole = target.Role;
        target.ChangeRole(MemberRole.Viewer);
        RaiseDomainEvent(new MemberRoleChangedEvent(Id, targetUserId, oldRole, MemberRole.Viewer, demotedBy));
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
