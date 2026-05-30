using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Web.Services.NotificationsApi;

public interface INotificationsApi
{
    Task<NotificationFeedApiDto> GetFeedAsync(
        DateTime? beforeCreatedAt = null,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken ct = default);

    Task<long> GetUnreadCountAsync(CancellationToken ct = default);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(CancellationToken ct = default);
}
