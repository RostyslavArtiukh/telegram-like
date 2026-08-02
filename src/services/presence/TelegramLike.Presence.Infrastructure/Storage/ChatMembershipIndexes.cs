using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Presence.Infrastructure.Storage;

/// <summary>
/// (ChatId) on chat_memberships — the only query here that is not a composite-<c>_id</c>
/// lookup is the whole-chat revoke behind <c>ChatDeleted</c>, which matched on chat alone and
/// therefore scanned the memberships of every chat this service has materialized.
/// </summary>
/// <remarks>
/// This is also the declaration whose absence <see cref="MongoIndexInitializer"/>'s startup
/// warning was pointing at: Presence was the service that had never written an index
/// initializer ([TL-119]). <c>user_presence</c> needs nothing — every read and write of it is
/// by <c>_id</c>, and the online/typing hot path lives in Redis, not Mongo.
/// </remarks>
internal sealed class ChatMembershipIndexes : IMongoIndexes
{
    public string Collection => "chat_memberships";

    public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default) =>
        EnsureIndexesAsync(database, cancellationToken);

    // Exposed so integration tests apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var memberships = database.GetCollection<BsonDocument>("chat_memberships");
        var byChatIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("ChatId"),
            new CreateIndexOptions { Name = "memberships_by_chat" });

        return memberships.Indexes.CreateOneAsync(byChatIndex, cancellationToken: cancellationToken);
    }
}
