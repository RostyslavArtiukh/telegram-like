using MongoDB.Driver;
using StackExchange.Redis;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

namespace TelegramLike.Infrastructure.Tests.Fixtures;

public sealed class IntegrationContainersFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7")
        .WithReplicaSet()
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IMongoClient MongoClient { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;
    public IConnectionMultiplexer Redis { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartAsync(), _redis.StartAsync());

        // Force direct-connection: Testcontainers maps a random host port, but the replica set
        // config advertises localhost:27017 (the container-internal port). With topology
        // discovery enabled the driver follows that advertised address and times out.
        // directConnection=true keeps the driver pinned to the mapped endpoint; single-node
        // replica set transactions still work because the server itself remains primary.
        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;
        settings.ReplicaSetName = null;

        MongoClient = new MongoClient(settings);
        Database = MongoClient.GetDatabase($"tl_test_{Guid.NewGuid():N}");

        Redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        Redis?.Dispose();
        await _mongo.DisposeAsync();
        await _redis.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationContainersFixture>
{
    public const string Name = "integration";
}
