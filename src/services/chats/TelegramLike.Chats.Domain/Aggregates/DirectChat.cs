using TelegramLike.Chats.Domain.Entities;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Domain.Aggregates;

public sealed class DirectChat : Chat
{
    private DirectChat() { }

    private DirectChat(Guid id, Guid createdBy, DateTime createdAt)
        : base(id, ChatType.Direct, name: null, createdBy, createdAt) { }

    public static DirectChat Create(Guid id, Guid initiatorUserId, Guid peerUserId)
    {
        // Caller-supplied id doubles as the idempotency key (see ChatRepository.AddAsync).
        if (id == Guid.Empty) throw new ArgumentException("Chat id cannot be empty.", nameof(id));
        if (initiatorUserId == peerUserId)
            throw new InvalidOperationException("Direct chat requires two distinct users.");

        var chat = new DirectChat(id, initiatorUserId, DateTime.UtcNow);
        var initiator = Member.Join(initiatorUserId, MemberRole.Member);
        var peer = Member.Join(peerUserId, MemberRole.Member);
        chat._members.Add(initiator);
        chat._members.Add(peer);

        chat.RaiseDomainEvent(new ChatCreatedEvent(chat.Id, ChatType.Direct, initiatorUserId));
        chat.RaiseDomainEvent(new MemberJoinedEvent(chat.Id, initiatorUserId, MemberRole.Member, chat.RecipientsExcept(initiatorUserId)));
        chat.RaiseDomainEvent(new MemberJoinedEvent(chat.Id, peerUserId, MemberRole.Member, chat.RecipientsExcept(peerUserId)));
        return chat;
    }

    public static DirectChat Reconstitute(
        Guid id, Guid createdBy, DateTime createdAt, DateTime? deletedAt, IEnumerable<Member> members)
    {
        var chat = new DirectChat(id, createdBy, createdAt) { DeletedAt = deletedAt };
        chat._members.AddRange(members);
        return chat;
    }

    public override void Rename(ChatName newName, Guid renamedBy)
        => throw new InvalidOperationException("DirectChat cannot be renamed.");

    public override void Delete(Guid deletedBy)
        => throw new InvalidOperationException("DirectChat cannot be deleted.");

    public override void Leave(Guid userId)
        => throw new InvalidOperationException("DirectChat does not support Leave.");

    public override void Kick(Guid targetUserId, Guid kickedBy)
        => throw new InvalidOperationException("DirectChat does not support Kick.");
}
