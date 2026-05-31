using TelegramLike.Chats.Domain.Entities;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Domain.Aggregates;

public abstract class Chat : AggregateRoot
{
    protected readonly List<Member> _members = [];

    public ChatType Type { get; protected set; }
    public ChatName? Name { get; protected set; }
    public Guid CreatedBy { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }

    public IReadOnlyList<Member> Members => _members.AsReadOnly();
    public IEnumerable<Member> ActiveMembers => _members.Where(m => m.IsActive);
    public bool IsDeleted => DeletedAt.HasValue;

    protected Chat() { }

    protected Chat(Guid id, ChatType type, ChatName? name, Guid createdBy, DateTime createdAt) : base(id)
    {
        Type = type;
        Name = name;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Member? FindActiveMember(Guid userId)
        => _members.FirstOrDefault(m => m.UserId == userId && m.IsActive);

    public Member? FindAnyMember(Guid userId)
        => _members.FirstOrDefault(m => m.UserId == userId);

    protected Member RequireActiveMember(Guid userId)
        => FindActiveMember(userId)
           ?? throw new InvalidOperationException($"User {userId} is not an active member of this chat.");

    internal IReadOnlyList<Guid> RecipientsExcept(Guid actorUserId)
        => ActiveMembers.Where(m => m.UserId != actorUserId).Select(m => m.UserId).ToList();

    protected void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Chat is deleted.");
    }

    public virtual void Rename(ChatName newName, Guid renamedBy)
    {
        EnsureNotDeleted();
        var actor = RequireActiveMember(renamedBy);
        if (actor.Role != MemberRole.Owner && actor.Role != MemberRole.Admin)
            throw new InvalidOperationException("Only Owner or Admin can rename a chat.");

        if (Name is null)
            throw new InvalidOperationException("This chat type does not support a name.");

        var oldName = Name.Value;
        Name = newName;
        RaiseDomainEvent(new ChatRenamedEvent(Id, oldName, newName.Value, renamedBy));
    }

    public virtual void Delete(Guid deletedBy)
    {
        EnsureNotDeleted();
        var actor = RequireActiveMember(deletedBy);
        if (actor.Role != MemberRole.Owner)
            throw new InvalidOperationException("Only Owner can delete a chat.");

        DeletedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ChatDeletedEvent(Id, deletedBy));
    }

    public abstract void Leave(Guid userId);
    public abstract void Kick(Guid targetUserId, Guid kickedBy);
}
