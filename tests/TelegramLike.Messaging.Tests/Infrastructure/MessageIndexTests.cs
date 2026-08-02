using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

/// <summary>
/// The (ChatId, SentAt desc) index is what keeps <c>GetChatMessages</c> — the hottest read in
/// the system — off a collection scan. Asserting the index merely exists would not catch the
/// regression that matters, so the second test asks Mongo for the actual query plan of the
/// paging query: a plan that falls back to COLLSCAN still returns correct results on a small
/// test database, which is exactly why this ceiling stayed invisible.
/// </summary>
[Collection(MongoCollection.Name)]
public class MessageIndexTests(MongoFixture fx)
{
    private const string IndexName = "chat_messages_by_recency";

    // A private database per test: indexes are per-collection state.
    private IMongoDatabase NewDatabase()
        => fx.MongoClient.GetDatabase($"tl_messaging_idx_test_{Guid.NewGuid():N}");

    [Fact]
    public async Task EnsureIndexes_CreatesTheChatRecencyIndex()
    {
        var database = NewDatabase();

        await MessageIndexes.EnsureIndexesAsync(database);

        var all = await (await database.GetCollection<BsonDocument>("messages").Indexes.ListAsync()).ToListAsync();
        var index = all.FirstOrDefault(i => i["name"].AsString == IndexName);
        index.Should().NotBeNull();

        var key = index!["key"].AsBsonDocument;
        key.Names.Should().Equal("ChatId", "SentAt");
        // Descending SentAt matches the query's sort direction, so Mongo walks the index
        // instead of collecting the chat's whole history and sorting it in memory.
        key["SentAt"].ToInt32().Should().Be(-1);
    }

    [Fact]
    public async Task TheChatPagingQuery_IsServedByTheIndex_NotACollectionScan()
    {
        var database = NewDatabase();
        await MessageIndexes.EnsureIndexesAsync(database);

        var chatId = Guid.NewGuid();
        var messages = database.GetCollection<MessageDocument>("messages");
        await messages.InsertManyAsync(Enumerable.Range(0, 50).Select(i => new MessageDocument
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            AuthorId = Guid.NewGuid(),
            Text = $"m{i}",
            SentAt = DateTime.UtcNow.AddSeconds(-i)
        }));

        // Exactly what MessageQueryService.GetChatMessagesAsync issues: one chat, keyset
        // cursor on SentAt, newest first, page size + 1.
        var explain = await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            {
                "explain", new BsonDocument
                {
                    { "find", "messages" },
                    {
                        "filter", new BsonDocument
                        {
                            { "ChatId", chatId.ToString() },
                            { "SentAt", new BsonDocument("$lt", DateTime.UtcNow) }
                        }
                    },
                    { "sort", new BsonDocument("SentAt", -1) },
                    { "limit", 21 }
                }
            },
            { "verbosity", "queryPlanner" }
        });

        // The winning plan's shape differs between query engines (classic vs. SBE), so match
        // on its content rather than on a fixed path: our index must appear, and the two
        // stages that mean "we gave up on the index" must not.
        var winningPlan = explain["queryPlanner"]["winningPlan"].ToJson();
        winningPlan.Should().Contain(IndexName);
        winningPlan.Should().NotContain("COLLSCAN");
        winningPlan.Should().NotContain("SORT");
    }
}
