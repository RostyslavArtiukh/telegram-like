using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Storage;

internal sealed class MongoChatMembershipReadModel(IMongoDatabase database) : IChatMembershipReadModel
{
    private readonly IMongoCollection<ChatMembershipDocument> _chatMembershipsCollection =
        database.GetCollection<ChatMembershipDocument>("chat_memberships");

    public async Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        var id = ChatMembershipDocument.MakeId(chatId, userId);
        // `IsActive != false` counts a missing field (legacy docs) as active.
        var filter = Builders<ChatMembershipDocument>.Filter.And(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, id),
            Builders<ChatMembershipDocument>.Filter.Ne(d => d.IsActive, false));
        return await _chatMembershipsCollection.Find(filter).Limit(1).AnyAsync(cancellationToken);
    }

    public Task UpsertActiveAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken cancellationToken = default)
        => ApplyAsync(chatId, userId, isActive: true, occurredAt, cancellationToken);

    public Task DeactivateAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken cancellationToken = default)
        => ApplyAsync(chatId, userId, isActive: false, occurredAt, cancellationToken);

    // Chat-wide deactivation for ChatDeleted. Same last-writer-wins guard as ApplyAsync, but
    // UpdateMany over every row of the chat and never an upsert: a chat nobody materialized
    // here has no membership to revoke, and inventing rows for it would be meaningless.
    public Task DeactivateChatAsync(Guid chatId, DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        var occurred = new BsonDateTime(occurredAt);
        var isNewer = IsNewerThanStored(occurred);

        var set = new BsonDocument("$set", new BsonDocument
        {
            { "IsActive", new BsonDocument("$cond", new BsonArray
                { isNewer, false, new BsonDocument("$ifNull", new BsonArray { "$IsActive", true }) }) },
            { "LastEventAt", new BsonDocument("$cond", new BsonArray
                { isNewer, occurred, new BsonDocument("$ifNull", new BsonArray { "$LastEventAt", occurred }) }) },
        });

        var pipeline = Builders<ChatMembershipDocument>.Update.Pipeline(
            PipelineDefinition<ChatMembershipDocument, ChatMembershipDocument>.Create(set));

        return _chatMembershipsCollection.UpdateManyAsync(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.ChatId, chatId),
            pipeline,
            cancellationToken: cancellationToken);
    }

    // Last-writer-wins by occurredAt via a conditional pipeline update: the new state
    // is applied only when occurredAt is newer than the stored LastEventAt (missing =>
    // epoch). A stale event is a no-op, never a resurrect/delete. One atomic upsert,
    // so concurrent/redelivered events can't interleave into a wrong final state.
    private Task ApplyAsync(Guid chatId, Guid userId, bool isActive, DateTime occurredAt, CancellationToken cancellationToken)
    {
        var id = ChatMembershipDocument.MakeId(chatId, userId);
        var occurred = new BsonDateTime(occurredAt);
        var isNewer = IsNewerThanStored(occurred);

        var set = new BsonDocument("$set", new BsonDocument
        {
            { "ChatId", chatId.ToString() },
            { "UserId", userId.ToString() },
            { "IsActive", new BsonDocument("$cond", new BsonArray
                { isNewer, isActive, new BsonDocument("$ifNull", new BsonArray { "$IsActive", true }) }) },
            { "LastEventAt", new BsonDocument("$cond", new BsonArray
                { isNewer, occurred, new BsonDocument("$ifNull", new BsonArray { "$LastEventAt", occurred }) }) },
        });

        var pipeline = Builders<ChatMembershipDocument>.Update.Pipeline(
            PipelineDefinition<ChatMembershipDocument, ChatMembershipDocument>.Create(set));

        return _chatMembershipsCollection.UpdateOneAsync(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, id),
            pipeline,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    private static BsonDocument IsNewerThanStored(BsonDateTime occurred)
        => new("$gte", new BsonArray
        {
            occurred,
            new BsonDocument("$ifNull", new BsonArray { "$LastEventAt", BsonDateTime.Create(DateTime.UnixEpoch) })
        });
}
