using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Messaging.Infrastructure.Storage;

/// <summary>
/// (ChatId, SentAt desc) on messages — the index behind the single hottest query in the whole
/// system: <c>GetChatMessages</c> filters one chat and keyset-pages by <c>SentAt</c> descending.
/// </summary>
/// <remarks>
/// Without it that query is a collection scan over every message ever sent, plus an in-memory
/// sort — and Mongo aborts a non-indexed sort once it exceeds 32 MB of working set, so chat
/// history stops opening at all well before any architectural limit is reached. The failure is
/// also invisible while a database is small: the scan is fast on a few thousand documents and
/// only degrades with traffic.
/// <para>
/// Descending on <c>SentAt</c> matches the sort direction so Mongo walks the index rather than
/// sorting; the cursor predicate (<c>SentAt &lt; before</c>) is a range on the same key, so a
/// page is one contiguous index range no matter how deep the paging goes.
/// </para>
/// </remarks>
internal sealed class MessageIndexes : IMongoIndexes
{
    public string Collection => "messages";

    public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default) =>
        EnsureIndexesAsync(database, cancellationToken);

    // Exposed so integration tests apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var messages = database.GetCollection<BsonDocument>("messages");
        var chatRecencyIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("ChatId").Descending("SentAt"),
            new CreateIndexOptions { Name = "chat_messages_by_recency" });

        return messages.Indexes.CreateOneAsync(chatRecencyIndex, cancellationToken: cancellationToken);
    }
}
