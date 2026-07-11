using FluentAssertions;
using TelegramLike.Presence.Infrastructure.Caching;
using TelegramLike.Presence.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Presence.Tests.Infrastructure;

[Collection(RedisCollection.Name)]
public class RedisTypingIndicatorIntegrationTests(RedisFixture fx)
{
    private RedisTypingIndicatorService NewService(TimeSpan ttl) => new(fx.Redis, ttl);

    [Fact]
    public async Task StartTyping_then_GetTypingUserIds_returns_user()
    {
        var svc = NewService(TimeSpan.FromSeconds(5));
        var chat = Guid.NewGuid();
        var user = Guid.NewGuid();

        await svc.StartTypingAsync(chat, user);

        (await svc.GetTypingUserIdsAsync(chat)).Should().ContainSingle().Which.Should().Be(user);
    }

    [Fact]
    public async Task StopTyping_removes_user_from_set()
    {
        var svc = NewService(TimeSpan.FromSeconds(5));
        var chat = Guid.NewGuid();
        var user = Guid.NewGuid();
        await svc.StartTypingAsync(chat, user);

        await svc.StopTypingAsync(chat, user);

        (await svc.GetTypingUserIdsAsync(chat)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetTypingUserIds_isolates_by_chat()
    {
        var svc = NewService(TimeSpan.FromSeconds(5));
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();
        var user = Guid.NewGuid();
        await svc.StartTypingAsync(chatA, user);

        (await svc.GetTypingUserIdsAsync(chatB)).Should().BeEmpty();
    }
}
