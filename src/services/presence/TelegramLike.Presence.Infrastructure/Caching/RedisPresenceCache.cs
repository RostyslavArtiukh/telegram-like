using StackExchange.Redis;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Caching;

internal sealed class RedisPresenceCache(IConnectionMultiplexer redis, TimeSpan heartbeatTtl) : IPresenceCache
{
    private readonly IDatabase _redisDatabase = redis.GetDatabase();

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _redisDatabase.KeyExistsAsync(Key(userId));

    public async Task<IReadOnlyDictionary<Guid, bool>> AreOnlineAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, bool>();

        var keys = userIds.Select(id => (RedisKey)Key(id)).ToArray();
        var values = await _redisDatabase.StringGetAsync(keys);

        var result = new Dictionary<Guid, bool>(userIds.Count);
        var i = 0;
        foreach (var id in userIds)
        {
            result[id] = values[i].HasValue;
            i++;
        }
        return result;
    }

    public Task TouchAsync(Guid userId, CancellationToken cancellationToken = default)
        => _redisDatabase.StringSetAsync(Key(userId), "online", heartbeatTtl);

    public Task ClearAsync(Guid userId, CancellationToken cancellationToken = default)
        => _redisDatabase.KeyDeleteAsync(Key(userId));

    private static string Key(Guid userId) => $"presence:{userId}";
}
