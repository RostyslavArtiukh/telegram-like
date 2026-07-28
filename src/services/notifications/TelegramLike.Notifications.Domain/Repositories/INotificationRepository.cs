using TelegramLike.Notifications.Domain.Aggregates;

namespace TelegramLike.Notifications.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<int> AddManyIgnoringDuplicatesAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default);

    Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);

    // Return the number of rows actually flipped to Read so callers can skip
    // publishing an UnreadCountChanged signal when nothing changed.
    Task<long> MarkAllAsReadAsync(Guid recipientId, DateTime readAt, CancellationToken cancellationToken = default);

    Task<long> MarkAllForChatAsReadAsync(Guid recipientId, Guid chatId, DateTime readAt, CancellationToken cancellationToken = default);
}
