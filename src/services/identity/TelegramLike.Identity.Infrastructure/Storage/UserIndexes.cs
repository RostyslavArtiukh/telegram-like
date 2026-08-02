using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Identity.Infrastructure.Storage;

/// <summary>
/// Unique indexes on Email and Username — the race backstop behind the check-then-act in
/// <c>RegisterUserCommandHandler</c>: two concurrent registrations for the same email/username
/// can both pass <c>ExistsBy*</c> and reach <c>AddAsync</c>, so uniqueness must be enforced by
/// the database. The case-insensitive collation (locale=en, strength=2) also makes "Alice" and
/// "alice" collide, matching the already-lowercased Email value object.
/// </summary>
internal sealed class UserIndexes : IMongoIndexes
{
    public string Collection => "users";

    public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default) =>
        EnsureIndexesAsync(database, cancellationToken);

    // Exposed so integration tests apply the same indexes as production.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>("users");
        var collation = new Collation("en", strength: CollationStrength.Secondary);

        return collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("Email"),
                new CreateIndexOptions { Name = "uniq_email", Unique = true, Collation = collation }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("Username"),
                new CreateIndexOptions { Name = "uniq_username", Unique = true, Collation = collation }),
        ], cancellationToken);
    }
}
