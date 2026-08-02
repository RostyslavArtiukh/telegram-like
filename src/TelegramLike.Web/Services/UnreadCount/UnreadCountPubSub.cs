namespace TelegramLike.Web.Services.UnreadCount;

/// Notifies NavMenu (per-user) that unread count for the user changed —
/// pushed by the Notifications service via RabbitMQ. NavMenu subscribes for
/// the current authenticated user's id and refetches its badge.
internal sealed class UnreadCountPubSub
{
    private readonly CircuitTopics<Func<Task>> _byUser = new();

    public IDisposable Subscribe(Guid userId, Func<Task> onChanged) =>
        _byUser.Subscribe(userId, onChanged);

    public Task PublishAsync(Guid userId) =>
        _byUser.PublishAsync(userId, onChanged => onChanged());
}
