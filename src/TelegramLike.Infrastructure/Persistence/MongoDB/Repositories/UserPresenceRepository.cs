using MongoDB.Driver;
using TelegramLike.Domain.Presence.Aggregates;
using TelegramLike.Domain.Presence.Repositories;

namespace TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;

internal sealed class UserPresenceRepository(IMongoDatabase database) : IUserPresenceRepository
{
    private readonly IMongoCollection<UserPresenceDocument> _presence =
        database.GetCollection<UserPresenceDocument>("user_presence");

    public async Task<UserPresence?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var doc = await _presence.Find(p => p.Id == userId).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task UpsertAsync(UserPresence presence, CancellationToken ct = default)
        => _presence.ReplaceOneAsync(
            Builders<UserPresenceDocument>.Filter.Eq(p => p.Id, presence.Id),
            UserPresenceDocument.FromDomain(presence),
            new ReplaceOptions { IsUpsert = true },
            ct);
}
