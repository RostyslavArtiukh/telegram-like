using MongoDB.Driver;

namespace TelegramLike.Shared.Infrastructure.Storage;

/// <summary>
/// One collection's indexes, declared by the service that owns the collection.
/// </summary>
/// <remarks>
/// Every service had invented its own <c>XIndexInitializer : IHostedService</c> — same scope,
/// same scaffolding, slightly different shape each time, and nothing that could tell you a
/// service had simply never written one. Presence, which queries <c>chat_memberships</c> on
/// every typing signal, is the service that never did. Declaring indexes instead of
/// hand-rolling a hosted service makes the set enumerable: <see cref="MongoIndexInitializer"/>
/// applies them all, logs what it applied, and says so when a service declares nothing.
/// <para>
/// Implementations must be idempotent — this runs on every start, and Mongo treats
/// re-creating an identical index as a no-op but rejects one whose options changed (see the
/// TTL <c>collMod</c> branch in <c>OutgoingEventsIndexes</c> for what that costs).
/// </para>
/// </remarks>
public interface IMongoIndexes
{
    /// <summary>Collection name, used in startup logging so a missing index is traceable.</summary>
    string Collection { get; }

    Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default);
}
