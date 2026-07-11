using System.Collections.Concurrent;

namespace TelegramLike.Web.Services.UnreadCount;

/// Notifies NavMenu (per-user) that unread count for the user changed —
/// pushed by the Notifications service via RabbitMQ. NavMenu subscribes for
/// the current authenticated user's id and refetches its badge.
internal sealed class UnreadCountPubSub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<Task>>> _subs = new();

    public IDisposable Subscribe(Guid userId, Func<Task> onChanged)
    {
        var token = Guid.NewGuid();
        var userSubs = _subs.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Func<Task>>());
        userSubs[token] = onChanged;
        return new Subscription(this, userId, token);
    }

    public async Task PublishAsync(Guid userId)
    {
        if (!_subs.TryGetValue(userId, out var userSubs)) return;

        foreach (var cb in userSubs.Values)
        {
            try { await cb(); }
            catch { /* one bad subscriber should not block others */ }
        }
    }

    private void Unsubscribe(Guid userId, Guid token)
    {
        if (_subs.TryGetValue(userId, out var userSubs))
            userSubs.TryRemove(token, out _);
    }

    private sealed class Subscription(UnreadCountPubSub owner, Guid userId, Guid token) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(userId, token);
    }
}
