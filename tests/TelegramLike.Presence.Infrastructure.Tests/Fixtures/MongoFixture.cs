using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace TelegramLike.Presence.Infrastructure.Tests.Fixtures;

public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7")
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
