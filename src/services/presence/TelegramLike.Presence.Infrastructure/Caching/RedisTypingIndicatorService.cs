using StackExchange.Redis;
using TelegramLike.Presence.Application.Abstractions;

namespace TelegramLike.Presence.Infrastructure.Caching;

// Typing state per chat is a Redis sorted set `typing:{chatId}` whose members are
// user ids and whose score is the expiry time (unix ms). This replaces the previous
// design of one `typing:{chatId}:{userId}` key per typer, whose read path had to
// KEYS/SCAN the whole keyspace (O(total keys), single-node). Reads now purge expired
// members with ZREMRANGEBYSCORE and return the rest — O(log n), one key per chat.
internal sealed class RedisTypingIndicatorService(IConnectionMultiplexer redis, TimeSpan typingTtl)
    : ITypingIndicatorService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task StartTypingAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var key = Key(chatId);
        var expiresAt = DateTimeOffset.UtcNow.Add(typingTtl).ToUnixTimeMilliseconds();
        await _db.SortedSetAddAsync(key, userId.ToString(), expiresAt);
        // Let the whole set self-clean once no one refreshes it; each start extends it.
        await _db.KeyExpireAsync(key, typingTtl + TimeSpan.FromSeconds(1));
    }

    public Task StopTypingAsync(Guid chatId, Guid userId, CancellationToken ct = default)
        => _db.SortedSetRemoveAsync(Key(chatId), userId.ToString());

    public async Task<IReadOnlyList<Guid>> GetTypingUserIdsAsync(Guid chatId, CancellationToken ct = default)
    {
        var key = Key(chatId);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Drop anyone whose typing window already lapsed, then read the survivors.
        await _db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, nowMs);
        var members = await _db.SortedSetRangeByRankAsync(key);

        var userIds = new List<Guid>(members.Length);
        foreach (var member in members)
            if (Guid.TryParse(member, out var userId))
                userIds.Add(userId);
        return userIds;
    }

    private static string Key(Guid chatId) => $"typing:{chatId}";
}
