using StackExchange.Redis;
using TelegramLike.Identity.Application.Security;

namespace TelegramLike.Identity.Infrastructure.Caching;

internal sealed class RedisSessionService(IConnectionMultiplexer redis, TimeSpan sessionTtl) : ISessionService
{
    private readonly IDatabase _redisDatabase = redis.GetDatabase();

    public async Task<string> CreateSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _redisDatabase.StringSetAsync($"session:{token}", userId.ToString(), sessionTtl);
        return token;
    }

    public async Task<Guid?> GetUserIdAsync(string token, CancellationToken cancellationToken = default)
    {
        var value = await _redisDatabase.StringGetAsync($"session:{token}");
        if (!value.HasValue) return null;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public async Task DeleteSessionAsync(string token, CancellationToken cancellationToken = default) =>
        await _redisDatabase.KeyDeleteAsync($"session:{token}");
}
