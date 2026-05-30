using StackExchange.Redis;
using TelegramLike.Presence.Application.Abstractions;

namespace TelegramLike.Presence.Infrastructure.Caching;

internal sealed class RedisTypingIndicatorService(IConnectionMultiplexer redis, TimeSpan typingTtl)
    : ITypingIndicatorService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public Task StartTypingAsync(Guid chatId, Guid userId, CancellationToken ct = default)
        => _db.StringSetAsync(Key(chatId, userId), "1", typingTtl);

    public Task StopTypingAsync(Guid chatId, Guid userId, CancellationToken ct = default)
        => _db.KeyDeleteAsync(Key(chatId, userId));

    public async Task<IReadOnlyList<Guid>> GetTypingUserIdsAsync(Guid chatId, CancellationToken ct = default)
    {
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        var pattern = $"typing:{chatId}:*";

        var userIds = new List<Guid>();
        await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(ct))
        {
            var raw = ((string)key!).Substring($"typing:{chatId}:".Length);
            if (Guid.TryParse(raw, out var userId))
                userIds.Add(userId);
        }
        return userIds;
    }

    private static string Key(Guid chatId, Guid userId) => $"typing:{chatId}:{userId}";
}
