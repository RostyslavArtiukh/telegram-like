using System.Collections.Concurrent;

namespace TelegramLike.Web.Services.Typing;

internal sealed class TypingPubSub : ITypingPubSub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<Guid, Task>>> _subs = new();

    public IDisposable Subscribe(Guid chatId, Func<Guid, Task> onUserTyping)
    {
        var token = Guid.NewGuid();
        var chatSubs = _subs.GetOrAdd(chatId, _ => new ConcurrentDictionary<Guid, Func<Guid, Task>>());
        chatSubs[token] = onUserTyping;
        return new Subscription(this, chatId, token);
    }

    public async Task PublishAsync(Guid chatId, Guid userId)
    {
        if (!_subs.TryGetValue(chatId, out var chatSubs)) return;

        foreach (var cb in chatSubs.Values)
        {
            try { await cb(userId); }
            catch { /* one bad subscriber should not block others */ }
        }
    }

    private void Unsubscribe(Guid chatId, Guid token)
    {
        if (_subs.TryGetValue(chatId, out var chatSubs))
            chatSubs.TryRemove(token, out _);
    }

    private sealed class Subscription(TypingPubSub owner, Guid chatId, Guid token) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(chatId, token);
    }
}
