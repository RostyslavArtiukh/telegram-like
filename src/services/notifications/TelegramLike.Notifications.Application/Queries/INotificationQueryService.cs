namespace TelegramLike.Notifications.Application.Queries;

public interface INotificationQueryService
{
    Task<NotificationFeedDto> GetFeedAsync(
        Guid recipientId,
        DateTime? beforeCreatedAt,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<long> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default);
}
