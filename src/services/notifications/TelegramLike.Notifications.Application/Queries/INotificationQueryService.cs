namespace TelegramLike.Notifications.Application.Queries;

public interface INotificationQueryService
{
    Task<NotificationFeedDto> GetFeedAsync(
        Guid recipientId,
        DateTime? beforeCreatedAt,
        int pageSize,
        bool unreadOnly,
        CancellationToken ct = default);

    Task<long> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default);
}
