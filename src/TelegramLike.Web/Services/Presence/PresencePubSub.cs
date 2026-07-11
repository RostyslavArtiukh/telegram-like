using System.Collections.Concurrent;

namespace TelegramLike.Web.Services.Presence;

/// Bridges UserCameOnline / UserWentOffline integration events from the Presence
/// service into Blazor Server circuits. ChatView subscribes per UserId for every
/// chat member it renders and reacts to status flips without polling.
/// Heartbeat → online and explicit /presence/offline fire push events; browser
/// closes don't, so ChatView also keeps a low-rate (~30s) polling fallback to
/// catch presence that fell off via Redis TTL.
internal sealed class PresencePubSub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<bool, Task>>> _subs = new();

    public IDisposable Subscribe(Guid userId, Func<bool, Task> onPresenceChanged)
    {
        var token = Guid.NewGuid();
        var userSubs = _subs.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Func<bool, Task>>());
        userSubs[token] = onPresenceChanged;
        return new Subscription(this, userId, token);
    }

    public async Task PublishAsync(Guid userId, bool isOnline)
    {
        if (!_subs.TryGetValue(userId, out var userSubs)) return;

        foreach (var cb in userSubs.Values)
        {
            try { await cb(isOnline); }
            catch { /* one bad subscriber should not block others */ }
        }
    }

    private void Unsubscribe(Guid userId, Guid token)
    {
        if (_subs.TryGetValue(userId, out var userSubs))
            userSubs.TryRemove(token, out _);
    }

    private sealed class Subscription(PresencePubSub owner, Guid userId, Guid token) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(userId, token);
    }
}
