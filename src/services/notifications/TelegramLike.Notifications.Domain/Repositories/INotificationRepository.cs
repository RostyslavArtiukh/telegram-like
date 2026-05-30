using TelegramLike.Notifications.Domain.Aggregates;

namespace TelegramLike.Notifications.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);

    Task AddManyAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default);

    Task UpdateAsync(Notification notification, CancellationToken ct = default);

    Task MarkAllAsReadAsync(Guid recipientId, DateTime readAt, CancellationToken ct = default);

    Task MarkAllForChatAsReadAsync(Guid recipientId, Guid chatId, DateTime readAt, CancellationToken ct = default);
}
