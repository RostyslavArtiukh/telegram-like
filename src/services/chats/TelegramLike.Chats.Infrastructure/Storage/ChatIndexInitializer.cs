using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Infrastructure.Storage;

internal sealed class ChatIndexInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<ChatIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var pruned = await EnsureIndexesAsync(database, cancellationToken);

        if (pruned > 0)
            logger.LogWarning(
                "Pruned {Count} duplicate chat_members rows left by the pre-fix rejoin path.", pruned);

        logger.LogInformation("Chats indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Applies the Chats indexes, first clearing any duplicate membership rows that would
    /// block them. Exposed so integration tests apply the same indexes as production.
    /// </summary>
    /// <remarks>
    /// Unique (ChatId, UserId) on chat_members is the backstop behind <c>Member.Rejoin</c>:
    /// the aggregate now revives a member's existing row instead of inserting a second one,
    /// and the index guarantees no other path can reintroduce a duplicate. Duplicates make
    /// <c>FindAnyMember</c> order-dependent — which is how <c>Ban</c> could mark a stale
    /// Left row Banned while the member's live row stayed Active.
    /// </remarks>
    /// <returns>How many duplicate rows were pruned.</returns>
    public static async Task<long> EnsureIndexesAsync(
        IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var members = database.GetCollection<ChatMemberDocument>("chat_members");

        // Must run before the index is created — Mongo refuses to build a unique index
        // over a collection that already violates it, which would fail startup on exactly
        // the databases the fix is meant to repair.
        var pruned = await PruneDuplicateMembersAsync(members, cancellationToken);

        var memberIndex = new CreateIndexModel<ChatMemberDocument>(
            Builders<ChatMemberDocument>.IndexKeys.Ascending(m => m.ChatId).Ascending(m => m.UserId),
            new CreateIndexOptions { Name = "uniq_chat_member", Unique = true });

        await members.Indexes.CreateOneAsync(memberIndex, cancellationToken: cancellationToken);
        return pruned;
    }

    private static async Task<long> PruneDuplicateMembersAsync(
        IMongoCollection<ChatMemberDocument> members, CancellationToken cancellationToken)
    {
        var duplicateGroups = await members
            .Aggregate()
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument { { "ChatId", "$ChatId" }, { "UserId", "$UserId" } } },
                { "ids", new BsonDocument("$push", "$_id") }
            })
            .Match(new BsonDocument("ids.1", new BsonDocument("$exists", true)))
            .ToListAsync(cancellationToken);

        if (duplicateGroups.Count == 0) return 0;

        var doomed = new List<Guid>();
        foreach (var group in duplicateGroups)
        {
            var ids = group["ids"].AsBsonArray.Select(id => Guid.Parse(id.AsString)).ToList();
            var rows = await members
                .Find(Builders<ChatMemberDocument>.Filter.In(m => m.Id, ids))
                .ToListAsync(cancellationToken);

            var keeper = PickSurvivor(rows);
            doomed.AddRange(rows.Where(r => r.Id != keeper.Id).Select(r => r.Id));
        }

        if (doomed.Count == 0) return 0;

        var result = await members.DeleteManyAsync(
            Builders<ChatMemberDocument>.Filter.In(m => m.Id, doomed), cancellationToken);

        return result.DeletedCount;
    }

    // A ban outranks everything: discarding one would silently readmit a moderated user,
    // and that is the wrong direction to fail. Otherwise the active row wins, and among
    // equals the most recent join — which is the row the old rejoin path always wrote last.
    private static ChatMemberDocument PickSurvivor(List<ChatMemberDocument> rows)
        => rows
            .OrderByDescending(r => r.Status == MemberStatus.Banned)
            .ThenByDescending(r => r.Status == MemberStatus.Active)
            .ThenByDescending(r => r.JoinedAt)
            .First();
}
