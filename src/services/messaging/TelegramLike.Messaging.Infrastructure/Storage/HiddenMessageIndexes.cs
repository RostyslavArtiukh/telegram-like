using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Messaging.Infrastructure.Storage;

/// <summary>
/// (UserId, MessageId) on hidden_messages — every read path filters on exactly that pair:
/// the per-page "which of these did the reader hide", <c>GetMessageById</c>'s single check,
/// and <c>HideAsync</c>'s upsert.
/// </summary>
/// <remarks>
/// Leading with <c>UserId</c> keeps the "everything this user hid" prefix usable, which is
/// what the collection is naturally organised around; <c>MessageId</c> second turns the page
/// lookup into an index range instead of a scan of that user's whole hide history.
/// </remarks>
internal sealed class HiddenMessageIndexes : IMongoIndexes
{
    public string Collection => "hidden_messages";

    public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default) =>
        EnsureIndexesAsync(database, cancellationToken);

    // Exposed so integration tests apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var hidden = database.GetCollection<BsonDocument>("hidden_messages");
        var byUserIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("UserId").Ascending("MessageId"),
            new CreateIndexOptions { Name = "hidden_by_user_message" });

        return hidden.Indexes.CreateOneAsync(byUserIndex, cancellationToken: cancellationToken);
    }
}
