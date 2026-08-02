using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Messaging.Infrastructure.Storage;

/// <summary>
/// Unique (MessageId, MemberId) on message_read_receipts — the idempotency backstop for read
/// receipts: two concurrent MarkAsRead for the same reader both see "no receipt" and both
/// upsert-insert, producing duplicate receipts (and, for broadcast, a double-counted read).
/// The unique index makes the second insert a duplicate-key no-op.
/// </summary>
internal sealed class MessageIndexes : IMongoIndexes
{
    public string Collection => "message_read_receipts";

    public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default) =>
        EnsureIndexesAsync(database, cancellationToken);

    // Exposed so integration tests apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var receipts = database.GetCollection<BsonDocument>("message_read_receipts");
        var receiptIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("MessageId").Ascending("MemberId"),
            new CreateIndexOptions { Name = "uniq_message_member", Unique = true });

        return receipts.Indexes.CreateOneAsync(receiptIndex, cancellationToken: cancellationToken);
    }
}
