using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TelegramLike.Identity.Infrastructure.Persistence;

internal sealed class UserIndexInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<UserIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        await EnsureIndexesAsync(database, cancellationToken);
        logger.LogInformation("User unique indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Exposed so integration tests can apply the same indexes as production.
    // Unique indexes on Email and Username are the race backstop behind the
    // check-then-act in RegisterUserCommandHandler: two concurrent registrations
    // for the same email/username can both pass ExistsBy* and reach AddAsync, so
    // uniqueness must be enforced by the database. The case-insensitive collation
    // (locale=en, strength=2) also makes "Alice" and "alice" collide, matching the
    // already-lowercased Email value object.
    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<BsonDocument>("users");
        var collation = new Collation("en", strength: CollationStrength.Secondary);

        await collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("Email"),
                new CreateIndexOptions { Name = "uniq_email", Unique = true, Collation = collation }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("Username"),
                new CreateIndexOptions { Name = "uniq_username", Unique = true, Collation = collation }),
        ], ct);
    }
}
