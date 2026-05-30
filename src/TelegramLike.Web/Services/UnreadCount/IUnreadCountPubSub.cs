namespace TelegramLike.Web.Services.UnreadCount;

/// Notifies NavMenu (per-user) that unread count for the user changed —
/// pushed by the Notifications service via RabbitMQ. NavMenu subscribes for
/// the current authenticated user's id and refetches its badge.
public interface IUnreadCountPubSub
{
    IDisposable Subscribe(Guid userId, Func<Task> onChanged);

    Task PublishAsync(Guid userId);
}
