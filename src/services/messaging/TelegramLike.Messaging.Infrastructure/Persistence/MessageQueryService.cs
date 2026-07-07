using MongoDB.Driver;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Application.Queries;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class MessageQueryService(IMongoDatabase database) : IMessageQueryService
{
    private readonly IMongoCollection<MessageDocument> _messagesCollection =
        database.GetCollection<MessageDocument>("messages");

    private readonly IMongoCollection<HiddenMessageDocument> _hiddenMessagesCollection =
        database.GetCollection<HiddenMessageDocument>("hidden_messages");

    public async Task<MessagePageDto> GetChatMessagesAsync(
        Guid chatId,
        Guid requesterId,
        DateTime? beforeSentAt,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<MessageDocument>.Filter;
        var filter = filterBuilder.Eq(m => m.ChatId, chatId);
        if (beforeSentAt.HasValue)
            filter &= filterBuilder.Lt(m => m.SentAt, beforeSentAt.Value);

        var docs = await _messagesCollection
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = docs.Count > pageSize;
        if (hasMore) docs.RemoveAt(docs.Count - 1);

        var hiddenIds = await _hiddenMessagesCollection
            .Find(h => h.UserId == requesterId)
            .Project(h => h.MessageId)
            .ToListAsync(cancellationToken);
        var hiddenSet = hiddenIds.ToHashSet();

        var items = docs
            .Where(d => !hiddenSet.Contains(d.Id))
            .Select(MapMessage)
            .ToList();

        DateTime? nextCursor = hasMore ? docs[^1].SentAt : null;
        return new MessagePageDto(items, nextCursor);
    }

    public async Task<MessageDto?> GetMessageByIdAsync(Guid messageId, Guid requesterId, CancellationToken cancellationToken = default)
    {
        var doc = await _messagesCollection.Find(m => m.Id == messageId).FirstOrDefaultAsync(cancellationToken);
        if (doc is null) return null;

        var isHidden = await _hiddenMessagesCollection.Find(h => h.MessageId == messageId && h.UserId == requesterId).AnyAsync(cancellationToken);
        return isHidden ? null : MapMessage(doc);
    }

    private static MessageDto MapMessage(MessageDocument d) => new(
        d.Id,
        d.ChatId,
        d.AuthorId,
        d.Text,
        d.Attachments.Select(a => new AttachmentDto(a.Type, a.Url, a.SizeBytes, a.FileName)).ToList(),
        d.ReplyToId,
        d.ForwardRef?.OriginalMessageId,
        d.ForwardRef?.OriginalChatId,
        d.Reactions.Select(r => new ReactionDto(r.UserId, r.Emoji, r.AddedAt)).ToList(),
        d.IsRetracted,
        d.RetractedAt,
        d.RetractedBy,
        d.BroadcastReadCount,
        d.SentAt);
}
