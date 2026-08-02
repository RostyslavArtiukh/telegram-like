using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

/// <summary>
/// Hiding is per-reader, and a page of 20 messages can only ever intersect 20 hide rows —
/// but the query used to fetch every message the reader had ever hidden, in any chat, on
/// every page. That made the cost of a page grow with the lifetime of the account. These
/// tests pin both halves of the fix: the lookup is bounded to the page, and it is served by
/// an index rather than a scan of the reader's whole hide history.
/// </summary>
[Collection(MongoCollection.Name)]
public class HiddenMessageQueryTests(MongoFixture fx)
{
    private const string IndexName = "hidden_by_user_message";

    private IMongoDatabase NewDatabase()
        => fx.MongoClient.GetDatabase($"tl_messaging_hidden_test_{Guid.NewGuid():N}");

    private static async Task<IMongoDatabase> SeededDatabaseAsync(IMongoDatabase database)
    {
        await MessageIndexes.EnsureIndexesAsync(database);
        await HiddenMessageIndexes.EnsureIndexesAsync(database);
        return database;
    }

    private static Task InsertMessagesAsync(IMongoDatabase database, IEnumerable<MessageDocument> docs)
        => database.GetCollection<MessageDocument>("messages").InsertManyAsync(docs);

    private static Task HideAsync(IMongoDatabase database, Guid messageId, Guid userId)
        => new HiddenMessageRepository(database).HideAsync(messageId, userId);

    private static MessageDocument Message(Guid chatId, DateTime sentAt) => new()
    {
        Id = Guid.NewGuid(),
        ChatId = chatId,
        AuthorId = Guid.NewGuid(),
        Text = "hello",
        SentAt = sentAt
    };

    [Fact]
    public async Task ChatPage_ExcludesOnlyTheRequestersOwnHiddenMessages()
    {
        var database = await SeededDatabaseAsync(NewDatabase());
        var chatId = Guid.NewGuid();
        var reader = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();

        var now = DateTime.UtcNow;
        var mine = Message(chatId, now);
        var theirs = Message(chatId, now.AddSeconds(-1));
        var untouched = Message(chatId, now.AddSeconds(-2));
        await InsertMessagesAsync(database, [mine, theirs, untouched]);

        await HideAsync(database, mine.Id, reader);
        await HideAsync(database, theirs.Id, someoneElse);

        var page = await new MessageQueryService(database)
            .GetChatMessagesAsync(chatId, reader, beforeSentAt: null, pageSize: 20);

        page.Items.Select(i => i.MessageId).Should().BeEquivalentTo([theirs.Id, untouched.Id]);
    }

    [Fact]
    public async Task ChatPage_IsUnaffectedByWhatTheReaderHidInOtherChats()
    {
        var database = await SeededDatabaseAsync(NewDatabase());
        var chatId = Guid.NewGuid();
        var otherChatId = Guid.NewGuid();
        var reader = Guid.NewGuid();

        var visible = Message(chatId, DateTime.UtcNow);
        var elsewhere = Enumerable.Range(0, 50)
            .Select(i => Message(otherChatId, DateTime.UtcNow.AddSeconds(-i)))
            .ToList();
        await InsertMessagesAsync(database, elsewhere.Append(visible));

        foreach (var hidden in elsewhere)
            await HideAsync(database, hidden.Id, reader);

        var page = await new MessageQueryService(database)
            .GetChatMessagesAsync(chatId, reader, beforeSentAt: null, pageSize: 20);

        page.Items.Select(i => i.MessageId).Should().Equal(visible.Id);
    }

    [Fact]
    public async Task TheHiddenLookup_IsServedByTheIndex_NotACollectionScan()
    {
        var database = await SeededDatabaseAsync(NewDatabase());
        var reader = Guid.NewGuid();
        var pageIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();

        // Exactly what MessageQueryService now issues: this reader, restricted to the ids
        // on the page it just read.
        var explain = await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            {
                "explain", new BsonDocument
                {
                    { "find", "hidden_messages" },
                    {
                        "filter", new BsonDocument
                        {
                            { "UserId", reader.ToString() },
                            { "MessageId", new BsonDocument("$in", new BsonArray(pageIds.Select(id => id.ToString()))) }
                        }
                    }
                }
            },
            { "verbosity", "queryPlanner" }
        });

        var winningPlan = explain["queryPlanner"]["winningPlan"].ToJson();
        winningPlan.Should().Contain(IndexName);
        winningPlan.Should().NotContain("COLLSCAN");
    }
}
