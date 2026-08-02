using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Presence.Infrastructure.Storage;
using TelegramLike.Presence.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Presence.Tests.Infrastructure;

/// <summary>
/// Presence's first index declaration ([TL-123]) — this is the service the startup warning
/// added in [TL-119] was aimed at. Membership checks here are composite-_id point lookups, but
/// the whole-chat revoke behind ChatDeleted matches on chat alone and so scanned every
/// membership this service had materialized.
/// </summary>
[Collection(MongoCollection.Name)]
public class ChatMembershipIndexTests(MongoFixture fx)
{
    private const string IndexName = "memberships_by_chat";

    private IMongoDatabase NewDatabase()
        => fx.MongoClient.GetDatabase($"tl_pres_membership_idx_{Guid.NewGuid():N}");

    [Fact]
    public async Task EnsureIndexes_CreatesTheByChatIndex()
    {
        var database = NewDatabase();

        await ChatMembershipIndexes.EnsureIndexesAsync(database);

        var all = await (await database.GetCollection<BsonDocument>("chat_memberships").Indexes.ListAsync())
            .ToListAsync();
        var index = all.FirstOrDefault(i => i["name"].AsString == IndexName);
        index.Should().NotBeNull();
        index!["key"].AsBsonDocument.Names.Should().Equal("ChatId");
    }

    [Fact]
    public async Task TheWholeChatRevoke_IsServedByTheIndex_NotACollectionScan()
    {
        var database = NewDatabase();
        await ChatMembershipIndexes.EnsureIndexesAsync(database);

        var chatId = Guid.NewGuid();

        var explain = await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            {
                "explain", new BsonDocument
                {
                    { "find", "chat_memberships" },
                    { "filter", new BsonDocument("ChatId", chatId.ToString()) }
                }
            },
            { "verbosity", "queryPlanner" }
        });

        var winningPlan = explain["queryPlanner"]["winningPlan"].ToJson();
        winningPlan.Should().Contain(IndexName);
        winningPlan.Should().NotContain("COLLSCAN");
    }
}
