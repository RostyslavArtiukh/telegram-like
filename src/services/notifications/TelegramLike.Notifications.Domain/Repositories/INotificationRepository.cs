using TelegramLike.Notifications.Domain.Aggregates;

namespace TelegramLike.Notifications.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);

    Task AddManyAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default);

    Task<int> AddManyIgnoringDuplicatesAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default);

    Task UpdateAsync(Notification notification, CancellationToken ct = default);

    // Return the number of rows actually flipped to Read so callers can skip
    // publishing an UnreadCountChanged signal when nothing changed.
    Task<long> MarkAllAsReadAsync(Guid recipientId, DateTime readAt, CancellationToken ct = default);

    Task<long> MarkAllForChatAsReadAsync(Guid recipientId, Guid chatId, DateTime readAt, CancellationToken ct = default);
}
