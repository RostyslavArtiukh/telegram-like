using MongoDB.Driver;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class MessageReadReceiptRepository(IMongoDatabase database) : IMessageReadReceiptRepository
{
    private readonly IMongoCollection<MessageReadReceiptDocument> _readReceiptsCollection =
        database.GetCollection<MessageReadReceiptDocument>("message_read_receipts");

    public async Task<bool> MarkAsReadAsync(Guid messageId, Guid memberId, DateTime readAt, CancellationToken cancellationToken = default)
    {
        var doc = new MessageReadReceiptDocument
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            MemberId = memberId,
            ReadAt = readAt
        };

        try
        {
            await _readReceiptsCollection.InsertOneAsync(doc, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Already read by this member (repeat call or concurrent race lost to the
            // unique index). Idempotent no-op — the first read time stands.
            return false;
        }
    }

    public Task<bool> HasReceiptAsync(Guid messageId, Guid memberId, CancellationToken cancellationToken = default)
        => _readReceiptsCollection.Find(r => r.MessageId == messageId && r.MemberId == memberId).AnyAsync(cancellationToken);
}
