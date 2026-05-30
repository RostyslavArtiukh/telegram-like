using MongoDB.Driver;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Application.Messaging.Queries;

namespace TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;

internal sealed class MessageQueryService(IMongoDatabase database) : IMessageQueryService
{
    private readonly IMongoCollection<MessageDocument> _messages =
        database.GetCollection<MessageDocument>("messages");

    private readonly IMongoCollection<HiddenMessageDocument> _hidden =
        database.GetCollection<HiddenMessageDocument>("hidden_messages");

    public async Task<MessagePageDto> GetChatMessagesAsync(
        Guid chatId,
        Guid requesterId,
        DateTime? beforeSentAt,
        int pageSize,
        CancellationToken ct = default)
    {
        var filterBuilder = Builders<MessageDocument>.Filter;
        var filter = filterBuilder.Eq(m => m.ChatId, chatId);
        if (beforeSentAt.HasValue)
            filter &= filterBuilder.Lt(m => m.SentAt, beforeSentAt.Value);

        var docs = await _messages
            .Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = docs.Count > pageSize;
        if (hasMore) docs.RemoveAt(docs.Count - 1);

        var hiddenIds = await _hidden
            .Find(h => h.UserId == requesterId)
            .Project(h => h.MessageId)
            .ToListAsync(ct);
        var hiddenSet = hiddenIds.ToHashSet();

        var items = docs
            .Where(d => !hiddenSet.Contains(d.Id))
            .Select(MapMessage)
            .ToList();

        DateTime? nextCursor = hasMore ? docs[^1].SentAt : null;
        return new MessagePageDto(items, nextCursor);
    }

    public async Task<MessageDto?> GetMessageByIdAsync(Guid messageId, Guid requesterId, CancellationToken ct = default)
    {
        var doc = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var isHidden = await _hidden.Find(h => h.MessageId == messageId && h.UserId == requesterId).AnyAsync(ct);
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
