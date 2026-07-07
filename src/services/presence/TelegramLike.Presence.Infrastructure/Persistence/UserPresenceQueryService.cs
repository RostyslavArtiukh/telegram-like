using MongoDB.Driver;
using TelegramLike.Presence.Application.Queries;

namespace TelegramLike.Presence.Infrastructure.Persistence;

internal sealed class UserPresenceQueryService(IMongoDatabase database) : IUserPresenceQueryService
{
    private readonly IMongoCollection<UserPresenceDocument> _userPresenceCollection =
        database.GetCollection<UserPresenceDocument>("user_presence");

    public async Task<UserPresenceDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var doc = await _userPresenceCollection.Find(p => p.Id == userId).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : Map(doc);
    }

    public async Task<IReadOnlyList<UserPresenceDto>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return [];

        var docs = await _userPresenceCollection
            .Find(Builders<UserPresenceDocument>.Filter.In(p => p.Id, userIds))
            .ToListAsync(cancellationToken);

        return docs.Select(Map).ToList();
    }

    private static UserPresenceDto Map(UserPresenceDocument doc) =>
        new(doc.Id, doc.Status, doc.LastSeenAt, doc.HideLastSeen);
}
