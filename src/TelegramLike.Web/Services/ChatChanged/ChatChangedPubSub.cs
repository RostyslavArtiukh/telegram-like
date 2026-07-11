using System.Collections.Concurrent;

namespace TelegramLike.Web.Services.ChatChanged;

/// Unified pubsub for "something in this chat changed and the message list
/// needs to redraw" — MessageRetracted / ReactionAdded / ReactionRemoved.
/// One subscription per ChatView, one callback (reload). New events live —
/// just add another consumer that publishes here.
internal sealed class ChatChangedPubSub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<Task>>> _subs = new();

    public IDisposable Subscribe(Guid chatId, Func<Task> onChanged)
    {
        var token = Guid.NewGuid();
        var chatSubs = _subs.GetOrAdd(chatId, _ => new ConcurrentDictionary<Guid, Func<Task>>());
        chatSubs[token] = onChanged;
        return new Subscription(this, chatId, token);
    }

    public async Task PublishAsync(Guid chatId)
    {
        if (!_subs.TryGetValue(chatId, out var chatSubs)) return;

        foreach (var cb in chatSubs.Values)
        {
            try { await cb(); }
            catch { /* one bad subscriber should not block others */ }
        }
    }

    private void Unsubscribe(Guid chatId, Guid token)
    {
        if (_subs.TryGetValue(chatId, out var chatSubs))
            chatSubs.TryRemove(token, out _);
    }

    private sealed class Subscription(ChatChangedPubSub owner, Guid chatId, Guid token) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(chatId, token);
    }
}
