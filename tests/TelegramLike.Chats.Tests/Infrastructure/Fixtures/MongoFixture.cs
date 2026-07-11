using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace TelegramLike.Chats.Tests.Infrastructure.Fixtures;

public sealed class MongoFixture : IAsyncLifetime
{
    // ChatRepository.Add/Update wrap chat + chat_members + outbox writes in a
    // multi-document transaction, which Mongo only supports on a replica set —
    // a plain standalone container throws "Standalone servers do not support
    // transactions".
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReplicaSet()
        .Build();

    public IMongoClient MongoClient { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        // Without DirectConnection the driver follows the advertised replica-set
        // host (only resolvable inside the container) and the connection dies.
        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;

        MongoClient = new MongoClient(settings);
        Database = MongoClient.GetDatabase($"tl_chats_test_{Guid.NewGuid():N}");
    }

    public async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "mongo";
}
