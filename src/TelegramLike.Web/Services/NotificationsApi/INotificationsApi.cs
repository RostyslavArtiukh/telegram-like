using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Web.Services.NotificationsApi;

public interface INotificationsApi
{
    Task<NotificationFeedApiDto> GetFeedAsync(
        Guid userId,
        DateTime? beforeCreatedAt = null,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken ct = default);

    Task<long> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
