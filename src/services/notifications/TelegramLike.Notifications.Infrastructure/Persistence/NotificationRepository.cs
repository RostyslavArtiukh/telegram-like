using MongoDB.Driver;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Persistence;

internal sealed class NotificationRepository(IMongoDatabase database) : INotificationRepository
{
    private readonly IMongoCollection<NotificationDocument> _notifications =
        database.GetCollection<NotificationDocument>("notifications");

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _notifications.Find(n => n.Id == id).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task AddAsync(Notification notification, CancellationToken ct = default)
        => _notifications.InsertOneAsync(NotificationDocument.FromDomain(notification), cancellationToken: ct);

    public Task AddManyAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
    {
        if (notifications.Count == 0) return Task.CompletedTask;

        var docs = notifications.Select(NotificationDocument.FromDomain).ToList();
        return _notifications.InsertManyAsync(docs, cancellationToken: ct);
    }

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
        => _notifications.ReplaceOneAsync(
            Builders<NotificationDocument>.Filter.Eq(n => n.Id, notification.Id),
            NotificationDocument.FromDomain(notification),
            new ReplaceOptions { IsUpsert = false },
            ct);

    public Task MarkAllAsReadAsync(Guid recipientId, DateTime readAt, CancellationToken ct = default)
    {
        var filter = Builders<NotificationDocument>.Filter.And(
            Builders<NotificationDocument>.Filter.Eq(n => n.RecipientId, recipientId),
            Builders<NotificationDocument>.Filter.Ne(n => n.Status, NotificationStatus.Read));

        var update = Builders<NotificationDocument>.Update
            .Set(n => n.Status, NotificationStatus.Read)
            .Set(n => n.ReadAt, readAt);

        return _notifications.UpdateManyAsync(filter, update, cancellationToken: ct);
    }

    public Task MarkAllForChatAsReadAsync(Guid recipientId, Guid chatId, DateTime readAt, CancellationToken ct = default)
    {
        var filter = Builders<NotificationDocument>.Filter.And(
            Builders<NotificationDocument>.Filter.Eq(n => n.RecipientId, recipientId),
            Builders<NotificationDocument>.Filter.Eq(n => n.Payload.ChatId, chatId),
            Builders<NotificationDocument>.Filter.Ne(n => n.Status, NotificationStatus.Read));

        var update = Builders<NotificationDocument>.Update
            .Set(n => n.Status, NotificationStatus.Read)
            .Set(n => n.ReadAt, readAt);

        return _notifications.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
