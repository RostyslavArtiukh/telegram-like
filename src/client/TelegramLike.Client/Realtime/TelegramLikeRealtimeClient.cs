using Microsoft.AspNetCore.SignalR.Client;
using TelegramLike.Client.Auth;
using TelegramLike.Contracts.Realtime;

namespace TelegramLike.Client.Realtime;

internal sealed class TelegramLikeRealtimeClient : ITelegramLikeRealtimeClient
{
    private readonly HubConnection _connection;
    private readonly HashSet<Guid> _joinedChats = [];
    private readonly object _joinedLock = new();

    public event Action<MessageSentPush>? MessageSent;
    public event Action<MessageSentPush>? ChatActivity;
    public event Action<MessageRetractedPush>? MessageRetracted;
    public event Action<ReactionPush>? ReactionAdded;
    public event Action<ReactionPush>? ReactionRemoved;
    public event Action<UserTypingPush>? UserTyping;
    public event Action<PresencePush>? PresenceChanged;
    public event Action? UnreadCountChanged;

    public TelegramLikeRealtimeClient(Uri gatewayBaseUrl, IAccessTokenProvider tokenProvider)
    {
        // The gateway strips /realtime and proxies the WebSocket to the hub's /hub.
        var hubUrl = new Uri(gatewayBaseUrl, "/realtime/hub");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Called on every negotiate (including reconnects), so an expired
                // 5-minute JWT is transparently re-exchanged by the provider.
                options.AccessTokenProvider = async () => await tokenProvider.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<MessageSentPush>(RealtimeEventNames.MessageSent, e => MessageSent?.Invoke(e));
        _connection.On<MessageSentPush>(RealtimeEventNames.ChatActivity, e => ChatActivity?.Invoke(e));
        _connection.On<MessageRetractedPush>(RealtimeEventNames.MessageRetracted, e => MessageRetracted?.Invoke(e));
        _connection.On<ReactionPush>(RealtimeEventNames.ReactionAdded, e => ReactionAdded?.Invoke(e));
        _connection.On<ReactionPush>(RealtimeEventNames.ReactionRemoved, e => ReactionRemoved?.Invoke(e));
        _connection.On<UserTypingPush>(RealtimeEventNames.UserTyping, e => UserTyping?.Invoke(e));
        _connection.On<PresencePush>(RealtimeEventNames.PresenceChanged, e => PresenceChanged?.Invoke(e));
        _connection.On(RealtimeEventNames.UnreadCountChanged, () => UnreadCountChanged?.Invoke());

        // A reconnect gets a fresh connection id, so server-side group membership
        // is gone — re-join every chat stream the app still has open.
        _connection.Reconnected += async _ =>
        {
            Guid[] chats;
            lock (_joinedLock) chats = [.. _joinedChats];
            foreach (var chatId in chats)
                await _connection.InvokeAsync("JoinChat", chatId);
        };
    }

    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    public Task ConnectAsync(CancellationToken ct = default) => _connection.StartAsync(ct);

    public Task DisconnectAsync(CancellationToken ct = default) => _connection.StopAsync(ct);

    public async Task JoinChatAsync(Guid chatId, CancellationToken ct = default)
    {
        await _connection.InvokeAsync("JoinChat", chatId, ct);
        lock (_joinedLock) _joinedChats.Add(chatId);
    }

    public async Task LeaveChatAsync(Guid chatId, CancellationToken ct = default)
    {
        lock (_joinedLock) _joinedChats.Remove(chatId);
        await _connection.InvokeAsync("LeaveChat", chatId, ct);
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
