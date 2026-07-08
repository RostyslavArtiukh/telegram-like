using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using TelegramLike.Client.Auth;
using TelegramLike.Contracts.Realtime;

namespace TelegramLike.Client.Realtime;

internal sealed class TelegramLikeRealtimeClient : ITelegramLikeRealtimeClient
{
    private readonly HubConnection _connection;
    // The byte value is unused — this is a concurrent set: joins/leaves and the
    // reconnect flush touch it from different threads without external locking.
    private readonly ConcurrentDictionary<Guid, byte> _joinedChats = new();
    private readonly SemaphoreSlim _connectGate = new(1, 1);

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
        _connection.Reconnected += async _ => await FlushJoinsAsync();
    }

    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) => EnsureConnectedAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => _connection.StopAsync(cancellationToken);

    public async Task JoinChatAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        // Record intent BEFORE touching the wire: if the hub is down or mid-connect,
        // the join is flushed once a connection is (re)established. This also means a
        // chat opened before the hub connected is not silently missed.
        _joinedChats.TryAdd(chatId, 0);

        await EnsureConnectedAsync(cancellationToken);
        try
        {
            if (_connection.State == HubConnectionState.Connected)
                await _connection.InvokeAsync("JoinChat", chatId, cancellationToken);
        }
        catch
        {
            // Retained in _joinedChats; FlushJoinsAsync re-joins on the next (re)connect.
        }
    }

    public async Task LeaveChatAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        _joinedChats.TryRemove(chatId, out _);
        try
        {
            if (_connection.State == HubConnectionState.Connected)
                await _connection.InvokeAsync("LeaveChat", chatId, cancellationToken);
        }
        catch
        {
            // Nothing to retain — we already dropped the intent.
        }
    }

    /// <summary>
    /// Starts the connection if it isn't already up, and flushes pending chat joins.
    /// Safe to call repeatedly: failures are swallowed so a hub that's down at login
    /// doesn't kill realtime for the whole session — the next call (e.g. opening a
    /// chat) retries. WithAutomaticReconnect only revives an already-established
    /// connection, so this action-driven retry is what covers a failed initial connect.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State == HubConnectionState.Connected) return;

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            // Only Disconnected can be started; Connecting/Reconnecting will resolve
            // on their own (and Reconnected flushes joins).
            if (_connection.State != HubConnectionState.Disconnected) return;

            await _connection.StartAsync(cancellationToken);
            await FlushJoinsAsync(cancellationToken);
        }
        catch
        {
            // Hub unreachable — retried on the next EnsureConnectedAsync.
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async Task FlushJoinsAsync(CancellationToken cancellationToken = default)
    {
        // Keys returns a stable snapshot, so a concurrent join/leave can't disrupt the loop.
        var chats = _joinedChats.Keys.ToArray();
        foreach (var chatId in chats)
        {
            try
            {
                await _connection.InvokeAsync("JoinChat", chatId, cancellationToken);
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                // One chat failing to re-join must not abort re-joining the rest — this
                // runs from Reconnected too, where an unguarded throw would silently drop
                // every remaining chat's live stream. The id stays in _joinedChats and is
                // retried on the next (re)connect flush.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connectGate.Dispose();
        await _connection.DisposeAsync();
    }
}
