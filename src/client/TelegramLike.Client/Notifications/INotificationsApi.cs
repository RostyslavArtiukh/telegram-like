using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Client.Notifications;

public interface INotificationsApi
{
    Task<NotificationFeedApiDto> GetFeedAsync(
        Guid userId,
        DateTime? beforeCreatedAt = null,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<long> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkChatAsReadAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);
}
