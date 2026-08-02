namespace TelegramLike.Web.Services.Presence;

/// Bridges UserCameOnline / UserWentOffline integration events from the Presence
/// service into Blazor Server circuits. ChatView subscribes per UserId for every
/// chat member it renders and reacts to status flips without polling.
/// Heartbeat → online and explicit /presence/offline fire push events; browser
/// closes don't, so ChatView also keeps a low-rate (~30s) polling fallback to
/// catch presence that fell off via Redis TTL.
internal sealed class PresencePubSub
{
    private readonly CircuitTopics<Func<bool, Task>> _byUser = new();

    public IDisposable Subscribe(Guid userId, Func<bool, Task> onPresenceChanged) =>
        _byUser.Subscribe(userId, onPresenceChanged);

    public Task PublishAsync(Guid userId, bool isOnline) =>
        _byUser.PublishAsync(userId, onPresenceChanged => onPresenceChanged(isOnline));
}
