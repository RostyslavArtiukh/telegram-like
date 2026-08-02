using System.Collections.Concurrent;

namespace TelegramLike.Web.Services;

/// <summary>
/// The subscription registry behind every <c>XPubSub</c>: a set of live callbacks per topic
/// (a chat id or a user id), fed by a MassTransit consumer and read by the Blazor circuits
/// currently rendering that topic.
/// </summary>
/// <remarks>
/// This is a **singleton on a stateful host**, so anything it keeps is kept for the process's
/// lifetime. Each pubsub used to hold a per-topic dictionary that was created on first
/// subscribe and never removed — so a replica accumulated one entry for every chat opened and
/// every user rendered since it started, and the count only ever went up. Small per entry, and
/// unbounded: it grew with *distinct topics ever seen*, not with open circuits, which is the
/// worst shape for something that already scales by tabs-per-instance ([TL-126]).
/// <para>
/// A topic is dropped when its last subscriber disposes. Add and remove take the topic's own
/// lock so a subscribe racing that removal cannot attach a callback to a dictionary that is
/// already detached from the map — a silently dead subscription, which for a chat view means
/// real-time simply stops working with nothing in the logs.
/// </para>
/// </remarks>
internal sealed class CircuitTopics<TCallback> where TCallback : Delegate
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, TCallback>> _topics = new();

    public IDisposable Subscribe(Guid topic, TCallback callback)
    {
        var token = Guid.NewGuid();

        while (true)
        {
            var subscribers = _topics.GetOrAdd(topic, _ => new ConcurrentDictionary<Guid, TCallback>());

            lock (subscribers)
            {
                // Still the instance the map holds? If a concurrent unsubscribe detached it
                // between the GetOrAdd and this lock, adding here would register a callback
                // no publish could ever reach. Retry against the fresh one.
                if (_topics.TryGetValue(topic, out var current) && ReferenceEquals(current, subscribers))
                {
                    subscribers[token] = callback;
                    return new Subscription(this, topic, token);
                }
            }
        }
    }

    /// <summary>
    /// Invokes every live callback for <paramref name="topic"/>. <paramref name="invoke"/>
    /// adapts the call, so each pubsub keeps its own callback shape.
    /// </summary>
    public async Task PublishAsync(Guid topic, Func<TCallback, Task> invoke)
    {
        if (!_topics.TryGetValue(topic, out var subscribers)) return;

        foreach (var callback in subscribers.Values)
        {
            try { await invoke(callback); }
            catch { /* one bad subscriber should not block others */ }
        }
    }

    /// <summary>Topics currently holding at least one subscriber.</summary>
    public int TopicCount => _topics.Count;

    private void Unsubscribe(Guid topic, Guid token)
    {
        if (!_topics.TryGetValue(topic, out var subscribers)) return;

        lock (subscribers)
        {
            subscribers.TryRemove(token, out _);

            // Remove only this exact instance: a subscriber that arrived on a replacement
            // dictionary must not be dropped along with the empty one.
            if (subscribers.IsEmpty)
                _topics.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<Guid, TCallback>>(topic, subscribers));
        }
    }

    private sealed class Subscription(CircuitTopics<TCallback> owner, Guid topic, Guid token) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(topic, token);
    }
}
