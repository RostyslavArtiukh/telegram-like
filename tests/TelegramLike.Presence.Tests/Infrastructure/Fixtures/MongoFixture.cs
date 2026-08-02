using MongoDB.Driver;
using Testcontainers.MongoDb;
using TelegramLike.Presence.Infrastructure.Storage;

namespace TelegramLike.Presence.Tests.Infrastructure.Fixtures;

public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .Build();

    public IMongoClient MongoClient { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;

        MongoClient = new MongoClient(settings);
        Database = MongoClient.GetDatabase($"tl_presence_test_{Guid.NewGuid():N}");

        // Same indexes as production.
        await ChatMembershipIndexes.EnsureIndexesAsync(Database);
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
