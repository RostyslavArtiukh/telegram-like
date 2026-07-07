using System.Collections.Concurrent;

namespace TelegramLike.Realtime.Api.Membership;

/// <summary>
/// In-memory, per-replica view of chat membership, materialized from the Chats
/// integration events this instance already consumes. Realtime has no database
/// (see the service CLAUDE.md), so this is deliberately ephemeral: a chat becomes
/// "known" only once an event for it has been observed. JoinChat fails closed for
/// known chats and fails open for unknown ones (e.g. right after a restart, before
/// events flow) — matching the rest of the system rather than locking members out.
/// </summary>
public interface IChatMembershipTracker
{
    bool IsKnownChat(Guid chatId);
    bool IsMember(Guid chatId, Guid userId);
    void Join(Guid chatId, Guid userId);
    void Leave(Guid chatId, Guid userId);
}

internal sealed class ChatMembershipTracker : IChatMembershipTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _membersByChat = new();

    public bool IsKnownChat(Guid chatId) => _membersByChat.ContainsKey(chatId);

    public bool IsMember(Guid chatId, Guid userId)
        => _membersByChat.TryGetValue(chatId, out var members) && members.ContainsKey(userId);

    public void Join(Guid chatId, Guid userId)
        => _membersByChat.GetOrAdd(chatId, _ => new ConcurrentDictionary<Guid, byte>()).TryAdd(userId, 0);

    public void Leave(Guid chatId, Guid userId)
    {
        if (_membersByChat.TryGetValue(chatId, out var members))
            members.TryRemove(userId, out _);
    }
}
