using System.Collections.Concurrent;

namespace TelegramLike.Realtime.Api.Membership;

/// <summary>
/// In-memory, per-replica view of chat membership, materialized from the Chats
/// integration events this instance already consumes. Realtime has no database
/// (see the service CLAUDE.md), so this is deliberately ephemeral: a chat becomes
/// "known" once an event for it has been observed, or once the admin backfill
/// re-publishes its membership snapshot ([TL-103]) — which is how a restarted replica
/// (whose temporary queue does not replay history) regains a full view. JoinChat fails
/// closed for known chats; an as-yet-unknown chat fails open (metadata-only exposure —
/// content is protected by Messaging's fail-closed reads) rather than locking members out.
/// </summary>
public sealed class ChatMembershipTracker
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
