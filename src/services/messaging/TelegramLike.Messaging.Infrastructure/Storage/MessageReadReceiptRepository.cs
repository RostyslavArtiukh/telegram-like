using MongoDB.Driver;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Storage;

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
}
