using MongoDB.Driver;
using TelegramLike.Notifications.Application.Queries;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Persistence;

internal sealed class NotificationQueryService(IMongoDatabase database) : INotificationQueryService
{
    private readonly IMongoCollection<NotificationDocument> _notifications =
        database.GetCollection<NotificationDocument>("notifications");

    public async Task<NotificationFeedDto> GetFeedAsync(
        Guid recipientId,
        DateTime? beforeCreatedAt,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<NotificationDocument>.Filter;
        var filter = filterBuilder.Eq(n => n.RecipientId, recipientId);

        if (unreadOnly)
            filter &= filterBuilder.Ne(n => n.Status, NotificationStatus.Read);

        if (beforeCreatedAt.HasValue)
            filter &= filterBuilder.Lt(n => n.CreatedAt, beforeCreatedAt.Value);

        var docs = await _notifications
            .Find(filter)
            .SortByDescending(n => n.CreatedAt)
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken);

        DateTime? nextCursor = null;
        if (docs.Count > pageSize)
        {
            nextCursor = docs[pageSize - 1].CreatedAt;
            docs.RemoveAt(docs.Count - 1);
        }

        var items = docs.Select(Map).ToList();
        return new NotificationFeedDto(items, nextCursor);
    }

    public Task<long> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<NotificationDocument>.Filter.And(
            Builders<NotificationDocument>.Filter.Eq(n => n.RecipientId, recipientId),
            Builders<NotificationDocument>.Filter.Ne(n => n.Status, NotificationStatus.Read));

        return _notifications.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    private static NotificationDto Map(NotificationDocument doc) => new(
        doc.Id,
        doc.RecipientId,
        doc.Type,
        doc.Payload.ChatId,
        doc.Payload.MessageId,
        doc.Payload.ActorId,
        doc.Status,
        doc.CreatedAt,
        doc.ReadAt);
}
