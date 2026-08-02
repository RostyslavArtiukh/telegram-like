using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

/// <summary>
/// GetActiveMemberIds runs on every send — it is both the membership check and the recipient
/// list — and it matches on chat, not on the composite _id the per-pair checks use. Without a
/// chat-leading index it scanned the memberships of every chat in the service to answer a
/// question about one.
/// </summary>
[Collection(MongoCollection.Name)]
public class ChatMembershipIndexTests(MongoFixture fx)
{
    private const string IndexName = "memberships_by_chat";

    private IMongoDatabase NewDatabase()
        => fx.MongoClient.GetDatabase($"tl_msg_membership_idx_{Guid.NewGuid():N}");

    [Fact]
    public async Task EnsureIndexes_CreatesTheByChatIndex()
    {
        var database = NewDatabase();

        await ChatMembershipIndexes.EnsureIndexesAsync(database);

        var all = await (await database.GetCollection<BsonDocument>("chat_memberships").Indexes.ListAsync())
            .ToListAsync();
        var index = all.FirstOrDefault(i => i["name"].AsString == IndexName);
        index.Should().NotBeNull();
        index!["key"].AsBsonDocument.Names.Should().Equal("ChatId", "IsActive", "UserId");
    }

    [Fact]
    public async Task TheSendPathsMembershipQuery_IsServedByTheIndex_NotACollectionScan()
    {
        var database = NewDatabase();
        await ChatMembershipIndexes.EnsureIndexesAsync(database);

        var chatId = Guid.NewGuid();
        await database.GetCollection<ChatMembershipDocument>("chat_memberships").InsertManyAsync(
            Enumerable.Range(0, 50).Select(_ =>
            {
                var userId = Guid.NewGuid();
                return new ChatMembershipDocument
                {
                    Id = ChatMembershipDocument.MakeId(chatId, userId),
                    ChatId = chatId,
                    UserId = userId,
                    IsActive = true,
                    LastEventAt = DateTime.UtcNow
                };
            }));

        // Exactly what MongoChatMembershipReadModel.GetActiveMemberIdsAsync issues: one chat,
        // active rows only (a missing IsActive counts as active), projecting just the user id.
        var explain = await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            {
                "explain", new BsonDocument
                {
                    { "find", "chat_memberships" },
                    {
                        "filter", new BsonDocument
                        {
                            { "ChatId", chatId.ToString() },
                            { "IsActive", new BsonDocument("$ne", false) }
                        }
                    },
                    { "projection", new BsonDocument { { "UserId", 1 }, { "_id", 0 } } }
                }
            },
            { "verbosity", "queryPlanner" }
        });

        var winningPlan = explain["queryPlanner"]["winningPlan"].ToJson();
        winningPlan.Should().Contain(IndexName);
        winningPlan.Should().NotContain("COLLSCAN");
    }
}
