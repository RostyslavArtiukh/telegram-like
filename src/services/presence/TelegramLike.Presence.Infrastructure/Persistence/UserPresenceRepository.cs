using MongoDB.Driver;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.Repositories;

namespace TelegramLike.Presence.Infrastructure.Persistence;

internal sealed class UserPresenceRepository(IMongoDatabase database) : IUserPresenceRepository
{
    private readonly IMongoCollection<UserPresenceDocument> _presence =
        database.GetCollection<UserPresenceDocument>("user_presence");

    public async Task<UserPresence?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var doc = await _presence.Find(p => p.Id == userId).FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public Task UpsertAsync(UserPresence presence, CancellationToken cancellationToken = default)
        => _presence.ReplaceOneAsync(
            Builders<UserPresenceDocument>.Filter.Eq(p => p.Id, presence.Id),
            UserPresenceDocument.FromDomain(presence),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
}
