using MongoDB.Driver;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Storage;

internal sealed class NotificationRepository(IMongoDatabase database) : INotificationRepository
{
    private readonly IMongoCollection<NotificationDocument> _notificationsCollection =
        database.GetCollection<NotificationDocument>("notifications");

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _notificationsCollection.Find(n => n.Id == id).FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        => _notificationsCollection.InsertOneAsync(NotificationDocument.FromDomain(notification), cancellationToken: cancellationToken);

    public Task AddManyAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0) return Task.CompletedTask;

        var docs = notifications.Select(NotificationDocument.FromDomain).ToList();
        return _notificationsCollection.InsertManyAsync(docs, cancellationToken: cancellationToken);
    }

    public async Task<int> AddManyIgnoringDuplicatesAsync(
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0) return 0;

        var docs = notifications.Select(NotificationDocument.FromDomain).ToList();
        try
        {
            await _notificationsCollection.InsertManyAsync(
                docs,
                new InsertManyOptions { IsOrdered = false },
                cancellationToken);
            return docs.Count;
        }
        catch (MongoBulkWriteException<NotificationDocument> ex)
        {
            // Duplicate-key (11000) errors come from the unique (RecipientId, SourceEventId) index
            // → safe to ignore: another redelivery of the same integration event already wrote this row.
            // Anything else is a real failure → rethrow.
            var nonDuplicate = ex.WriteErrors.Where(e => e.Category != ServerErrorCategory.DuplicateKey).ToList();
            if (nonDuplicate.Count > 0) throw;

            return docs.Count - ex.WriteErrors.Count;
        }
    }

    public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
        => _notificationsCollection.ReplaceOneAsync(
            Builders<NotificationDocument>.Filter.Eq(n => n.Id, notification.Id),
            NotificationDocument.FromDomain(notification),
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

    public async Task<long> MarkAllAsReadAsync(Guid recipientId, DateTime readAt, CancellationToken cancellationToken = default)
    {
        var filter = Builders<NotificationDocument>.Filter.And(
            Builders<NotificationDocument>.Filter.Eq(n => n.RecipientId, recipientId),
            Builders<NotificationDocument>.Filter.Ne(n => n.Status, NotificationStatus.Read));

        var update = Builders<NotificationDocument>.Update
            .Set(n => n.Status, NotificationStatus.Read)
            .Set(n => n.ReadAt, readAt);

        var result = await _notificationsCollection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<long> MarkAllForChatAsReadAsync(Guid recipientId, Guid chatId, DateTime readAt, CancellationToken cancellationToken = default)
    {
        var filter = Builders<NotificationDocument>.Filter.And(
            Builders<NotificationDocument>.Filter.Eq(n => n.RecipientId, recipientId),
            Builders<NotificationDocument>.Filter.Eq(n => n.Payload.ChatId, chatId),
            Builders<NotificationDocument>.Filter.Ne(n => n.Status, NotificationStatus.Read));

        var update = Builders<NotificationDocument>.Update
            .Set(n => n.Status, NotificationStatus.Read)
            .Set(n => n.ReadAt, readAt);

        var result = await _notificationsCollection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }
}
