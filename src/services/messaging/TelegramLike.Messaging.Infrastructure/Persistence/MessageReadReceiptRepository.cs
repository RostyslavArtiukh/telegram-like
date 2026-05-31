using MongoDB.Driver;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class MessageReadReceiptRepository(IMongoDatabase database) : IMessageReadReceiptRepository
{
    private readonly IMongoCollection<MessageReadReceiptDocument> _receipts =
        database.GetCollection<MessageReadReceiptDocument>("message_read_receipts");

    public async Task MarkAsReadAsync(Guid messageId, Guid memberId, DateTime readAt, CancellationToken ct = default)
    {
        var filter = Builders<MessageReadReceiptDocument>.Filter.And(
            Builders<MessageReadReceiptDocument>.Filter.Eq(r => r.MessageId, messageId),
            Builders<MessageReadReceiptDocument>.Filter.Eq(r => r.MemberId, memberId));

        var update = Builders<MessageReadReceiptDocument>.Update
            .SetOnInsert(r => r.Id, Guid.NewGuid())
            .SetOnInsert(r => r.MessageId, messageId)
            .SetOnInsert(r => r.MemberId, memberId)
            .Set(r => r.ReadAt, readAt);

        await _receipts.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public Task<bool> HasReceiptAsync(Guid messageId, Guid memberId, CancellationToken ct = default)
        => _receipts.Find(r => r.MessageId == messageId && r.MemberId == memberId).AnyAsync(ct);
}
