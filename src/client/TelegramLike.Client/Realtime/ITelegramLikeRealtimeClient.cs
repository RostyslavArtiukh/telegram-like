using TelegramLike.Contracts.Realtime;

namespace TelegramLike.Client.Realtime;

/// <summary>
/// Live push channel from the Realtime service's SignalR hub (via the gateway).
/// Connect once after login; join/leave a chat's stream while that chat is open.
/// Events fire on a background thread — UI hosts must marshal to their UI thread.
/// </summary>
public interface ITelegramLikeRealtimeClient : IAsyncDisposable
{
    /// <summary>New message in a chat this client has joined.</summary>
    event Action<MessageSentPush>? MessageSent;

    /// <summary>New message in any chat the current user belongs to (chat list / badges).</summary>
    event Action<MessageSentPush>? ChatActivity;

    event Action<MessageRetractedPush>? MessageRetracted;
    event Action<ReactionPush>? ReactionAdded;
    event Action<ReactionPush>? ReactionRemoved;
    event Action<UserTypingPush>? UserTyping;
    event Action<PresencePush>? PresenceChanged;

    /// <summary>Signal-only: refetch the unread count over HTTP.</summary>
    event Action? UnreadCountChanged;

    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task JoinChatAsync(Guid chatId, CancellationToken cancellationToken = default);
    Task LeaveChatAsync(Guid chatId, CancellationToken cancellationToken = default);
}
