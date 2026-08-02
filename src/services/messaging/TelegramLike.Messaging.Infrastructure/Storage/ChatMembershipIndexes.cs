using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Messaging.Infrastructure.Storage;

/// <summary>
/// (ChatId, IsActive, UserId) on chat_memberships — the chat-wide half of the read-model.
/// </summary>
/// <remarks>
/// Per-pair checks (<c>IsActiveMember</c>, <c>IsModerator</c>) look up the composite
/// <c>_id</c> and need nothing extra. What had no index at all is everything keyed by chat:
/// <c>GetActiveMemberIds</c> — which runs on <b>every single send</b>, since it is both the
/// membership check and the recipient list — plus <c>IsChatKnown</c> and the whole-chat
/// revoke behind <c>ChatDeleted</c>. Those were scanning the memberships of every chat in the
/// service to answer a question about one.
/// <para>
/// <c>UserId</c> trails the key so the send path's projection can be answered from the index
/// itself rather than fetching each membership document.
/// </para>
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
            Builders<BsonDocument>.IndexKeys.Ascending("ChatId").Ascending("IsActive").Ascending("UserId"),
            new CreateIndexOptions { Name = "memberships_by_chat" });

        return memberships.Indexes.CreateOneAsync(byChatIndex, cancellationToken: cancellationToken);
    }
}
