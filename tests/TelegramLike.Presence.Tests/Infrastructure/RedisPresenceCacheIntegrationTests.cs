using FluentAssertions;
using TelegramLike.Presence.Infrastructure.Caching;
using TelegramLike.Presence.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Presence.Tests.Infrastructure;

[Collection(RedisCollection.Name)]
public class RedisPresenceCacheIntegrationTests(RedisFixture fx)
{
    private RedisPresenceCache NewCache(TimeSpan ttl) => new(fx.Redis, ttl);

    [Fact]
    public async Task Touch_then_IsOnline_returns_true()
    {
        var cache = NewCache(TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();

        await cache.TouchAsync(userId);

        (await cache.IsOnlineAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task Clear_removes_presence_key()
    {
        var cache = NewCache(TimeSpan.FromSeconds(30));
        var userId = Guid.NewGuid();
        await cache.TouchAsync(userId);

        await cache.ClearAsync(userId);

        (await cache.IsOnlineAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task Touch_expires_after_ttl()
    {
        var cache = NewCache(TimeSpan.FromMilliseconds(500));
        var userId = Guid.NewGuid();
        await cache.TouchAsync(userId);
        (await cache.IsOnlineAsync(userId)).Should().BeTrue();

        await Task.Delay(TimeSpan.FromSeconds(1));

        (await cache.IsOnlineAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task AreOnline_returns_status_per_id_with_correct_alignment()
    {
        var cache = NewCache(TimeSpan.FromSeconds(30));
        var online = Guid.NewGuid();
        var offline = Guid.NewGuid();
        await cache.TouchAsync(online);

        var result = await cache.AreOnlineAsync(new[] { online, offline });

        result.Should().HaveCount(2);
        result[online].Should().BeTrue();
        result[offline].Should().BeFalse();
    }
}
