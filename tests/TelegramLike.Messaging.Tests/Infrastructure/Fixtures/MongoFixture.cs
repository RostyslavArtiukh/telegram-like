using MongoDB.Driver;
using Testcontainers.MongoDb;
using TelegramLike.Messaging.Infrastructure.Storage;

namespace TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

public sealed class MongoFixture : IAsyncLifetime
{
    // ChatMembershipDocument reads/writes are single-document, but MessageRepository
    // uses multi-document transactions (message + outbox), which Mongo only supports
    // on a replica set — a plain standalone container throws "Standalone servers do
    // not support transactions".
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReplicaSet()
        .Build();

    public IMongoClient MongoClient { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;

        MongoClient = new MongoClient(settings);
        Database = MongoClient.GetDatabase($"tl_messaging_test_{Guid.NewGuid():N}");

        // Same indexes as production, notably the unique (MessageId, MemberId) index
        // that makes MessageReadReceiptRepository.MarkAsReadAsync idempotent.
        await MessageIndexes.EnsureIndexesAsync(Database);
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
