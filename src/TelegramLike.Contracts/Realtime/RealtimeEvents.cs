namespace TelegramLike.Contracts.Realtime;

// Wire contracts between the Realtime service's SignalR hub and external clients
// (the TelegramLike.Client SDK). Server pushes these as camelCase JSON; both sides
// reference this file, so the shapes can't drift.

public sealed record MessageSentPush(Guid MessageId, Guid ChatId, Guid AuthorId);

public sealed record MessageRetractedPush(Guid MessageId, Guid ChatId, Guid RetractedBy);

public sealed record ReactionPush(Guid MessageId, Guid ChatId, Guid UserId, string Emoji);

public sealed record UserTypingPush(Guid ChatId, Guid UserId);

public sealed record PresencePush(Guid UserId, bool IsOnline);

/// <summary>
/// Client-method names the hub invokes. "MessageSent" targets the chat group
/// (open-chat view); "ChatActivity" targets per-user groups (chat list / badges),
/// so a client never receives the same semantic event twice.
/// </summary>
public static class RealtimeEventNames
{
    public const string MessageSent = "MessageSent";
    public const string ChatActivity = "ChatActivity";
    public const string MessageRetracted = "MessageRetracted";
    public const string ReactionAdded = "ReactionAdded";
    public const string ReactionRemoved = "ReactionRemoved";
    public const string UserTyping = "UserTyping";
    public const string PresenceChanged = "PresenceChanged";
    public const string UnreadCountChanged = "UnreadCountChanged";
}
