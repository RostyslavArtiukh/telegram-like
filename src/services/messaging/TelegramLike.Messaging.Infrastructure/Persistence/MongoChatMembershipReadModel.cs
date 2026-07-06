using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class MongoChatMembershipReadModel(IMongoDatabase database) : IChatMembershipReadModel
{
    private readonly IMongoCollection<ChatMembershipDocument> _memberships =
        database.GetCollection<ChatMembershipDocument>("chat_memberships");

    // `IsActive != false` counts a missing field (legacy docs) as active.
    private static FilterDefinition<ChatMembershipDocument> ActiveOf(Guid chatId)
        => Builders<ChatMembershipDocument>.Filter.And(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.ChatId, chatId),
            Builders<ChatMembershipDocument>.Filter.Ne(d => d.IsActive, false));

    public async Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var id = ChatMembershipDocument.MakeId(chatId, userId);
        var filter = Builders<ChatMembershipDocument>.Filter.And(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, id),
            Builders<ChatMembershipDocument>.Filter.Ne(d => d.IsActive, false));
        return await _memberships.Find(filter).Limit(1).AnyAsync(ct);
    }

    public async Task<bool> IsModeratorAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var id = ChatMembershipDocument.MakeId(chatId, userId);
        var filter = Builders<ChatMembershipDocument>.Filter.And(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, id),
            Builders<ChatMembershipDocument>.Filter.Ne(d => d.IsActive, false),
            Builders<ChatMembershipDocument>.Filter.In(d => d.Role, ["Owner", "Admin"]));
        return await _memberships.Find(filter).Limit(1).AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(Guid chatId, CancellationToken ct = default)
        => await _memberships
            .Find(ActiveOf(chatId))
            .Project(d => d.UserId)
            .ToListAsync(ct);

    public Task UpsertActiveAsync(Guid chatId, Guid userId, string? role, DateTime occurredAt, CancellationToken ct = default)
        => ApplyAsync(chatId, userId, isActive: true, role, occurredAt, ct);

    public Task DeactivateAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken ct = default)
        => ApplyAsync(chatId, userId, isActive: false, role: null, occurredAt, ct);

    public Task SetRoleAsync(Guid chatId, Guid userId, string role, DateTime occurredAt, CancellationToken ct = default)
        => ApplyAsync(chatId, userId, isActive: null, role, occurredAt, ct);

    // Last-writer-wins by occurredAt via a conditional pipeline update: each supplied
    // field is applied only when occurredAt is newer than the stored LastEventAt
    // (missing => epoch); a stale event is a no-op, never a resurrect/delete/role-revert.
    // isActive/role are null when the event doesn't touch that field (leave keeps role,
    // role-change keeps active state). One atomic upsert, so concurrent/redelivered
    // events can't interleave into a wrong final state.
    private Task ApplyAsync(Guid chatId, Guid userId, bool? isActive, string? role, DateTime occurredAt, CancellationToken ct)
    {
        var id = ChatMembershipDocument.MakeId(chatId, userId);
        var occurred = new BsonDateTime(occurredAt);
        var isNewer = new BsonDocument("$gte", new BsonArray
        {
            occurred,
            new BsonDocument("$ifNull", new BsonArray { "$LastEventAt", BsonDateTime.Create(DateTime.UnixEpoch) })
        });

        BsonValue activeExpr = isActive.HasValue
            ? new BsonDocument("$cond", new BsonArray
                { isNewer, isActive.Value, new BsonDocument("$ifNull", new BsonArray { "$IsActive", true }) })
            : new BsonDocument("$ifNull", new BsonArray { "$IsActive", true });

        BsonValue roleExpr = role is not null
            ? new BsonDocument("$cond", new BsonArray
                { isNewer, role, new BsonDocument("$ifNull", new BsonArray { "$Role", "Member" }) })
            : new BsonDocument("$ifNull", new BsonArray { "$Role", "Member" });

        var set = new BsonDocument("$set", new BsonDocument
        {
            { "ChatId", chatId.ToString() },
            { "UserId", userId.ToString() },
            { "IsActive", activeExpr },
            { "Role", roleExpr },
            { "LastEventAt", new BsonDocument("$cond", new BsonArray
                { isNewer, occurred, new BsonDocument("$ifNull", new BsonArray { "$LastEventAt", occurred }) }) },
        });

        var pipeline = Builders<ChatMembershipDocument>.Update.Pipeline(
            PipelineDefinition<ChatMembershipDocument, ChatMembershipDocument>.Create(set));

        return _memberships.UpdateOneAsync(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, id),
            pipeline,
            new UpdateOptions { IsUpsert = true },
            ct);
    }
}
