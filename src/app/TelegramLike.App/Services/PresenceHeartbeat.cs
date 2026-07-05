using TelegramLike.Client.Auth;
using TelegramLike.Client.Presence;

namespace TelegramLike.App.Services;

/// <summary>
/// Keeps the logged-in user "online": Presence's Redis key has a 30s TTL, so a
/// 20s heartbeat keeps it alive (same cadence as the Web BFF's PresenceHeartbeat
/// component). Started after login, stopped on sign-out.
/// </summary>
public sealed class PresenceHeartbeat(IPresenceApi presence, TelegramLikeSession session) : IAsyncDisposable
{
    private Timer? _timer;

    public void Start()
    {
        Stop();
        _timer = new Timer(_ => _ = BeatAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(20));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private async Task BeatAsync()
    {
        try
        {
            if (session.UserId is { } me)
                await presence.HeartbeatAsync(me);
        }
        catch
        {
            // Transient network failure just means the online dot may flicker;
            // the next beat retries.
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        try
        {
            if (session.UserId is { } me)
                await presence.GoOfflineAsync(me);
        }
        catch { /* best effort on shutdown */ }
    }
}
