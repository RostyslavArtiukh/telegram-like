using FluentAssertions;
using NSubstitute;
using TelegramLike.Client.Auth;
using TelegramLike.Client.Identity;

namespace TelegramLike.Client.Tests.Auth;

public class TelegramLikeSessionTests
{
    private static SessionExchangeResult ExchangeResult(Guid userId, string username = "alice", string accessToken = "jwt-1", int expiresIn = 3600)
        => new(userId, username, $"{username}@example.com", accessToken, expiresIn);

    [Fact]
    public async Task LoginAsync_StoresSessionToken_AndEagerlyExchanges()
    {
        var userId = Guid.NewGuid();
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        identityAuth.LoginAsync("a@b.com", "pw", Arg.Any<CancellationToken>()).Returns("session-token-1");
        identityAuth.ExchangeAsync("session-token-1", Arg.Any<CancellationToken>())
            .Returns(ExchangeResult(userId));

        var store = new InMemorySessionStore();
        var session = new TelegramLikeSession(identityAuth, store);

        await session.LoginAsync("a@b.com", "pw");

        (await store.GetSessionTokenAsync()).Should().Be("session-token-1");
        session.UserId.Should().Be(userId);
        session.Username.Should().Be("alice");
        await identityAuth.Received(1).ExchangeAsync("session-token-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WhenExchangeFails_ThrowsAndClearsStoredToken()
    {
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        identityAuth.LoginAsync("a@b.com", "pw", Arg.Any<CancellationToken>()).Returns("session-token-2");
        identityAuth.ExchangeAsync("session-token-2", Arg.Any<CancellationToken>())
            .Returns((SessionExchangeResult?)null);

        var store = new InMemorySessionStore();
        var session = new TelegramLikeSession(identityAuth, store);

        var act = async () => await session.LoginAsync("a@b.com", "pw");

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await store.GetSessionTokenAsync()).Should().BeNull();
        session.UserId.Should().BeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenNoSessionToken_ReturnsNullWithoutCallingExchange()
    {
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        var session = new TelegramLikeSession(identityAuth, new InMemorySessionStore());

        var token = await session.GetAccessTokenAsync();

        token.Should().BeNull();
        await identityAuth.DidNotReceive().ExchangeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenExchangeReturnsNull_ClearsStoredTokenAndReturnsNull()
    {
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        identityAuth.ExchangeAsync("stale-token", Arg.Any<CancellationToken>())
            .Returns((SessionExchangeResult?)null);

        var store = new InMemorySessionStore();
        await store.SetSessionTokenAsync("stale-token");
        var session = new TelegramLikeSession(identityAuth, store);

        var token = await session.GetAccessTokenAsync();

        token.Should().BeNull();
        (await store.GetSessionTokenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesAcrossConcurrentCalls_ExchangesOnlyOnce()
    {
        var userId = Guid.NewGuid();
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        identityAuth.ExchangeAsync("tok", Arg.Any<CancellationToken>())
            .Returns(ExchangeResult(userId, accessToken: "jwt-cached"));

        var store = new InMemorySessionStore();
        await store.SetSessionTokenAsync("tok");
        var session = new TelegramLikeSession(identityAuth, store);

        // Two concurrent calls before anything is cached must still exchange exactly
        // once — this is what the semaphore's double-checked-lock is protecting.
        var first = session.GetAccessTokenAsync();
        var second = session.GetAccessTokenAsync();
        var results = await Task.WhenAll(first, second);

        results.Should().AllBeEquivalentTo("jwt-cached");
        await identityAuth.Received(1).ExchangeAsync("tok", Arg.Any<CancellationToken>());

        // A subsequent call still hits the cache, not the exchange again.
        var third = await session.GetAccessTokenAsync();
        third.Should().Be("jwt-cached");
        await identityAuth.Received(1).ExchangeAsync("tok", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_ClearsStoredTokenAndCachedAccessToken()
    {
        var userId = Guid.NewGuid();
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        identityAuth.LoginAsync("a@b.com", "pw", Arg.Any<CancellationToken>()).Returns("session-token-3");
        identityAuth.ExchangeAsync("session-token-3", Arg.Any<CancellationToken>())
            .Returns(ExchangeResult(userId));

        var store = new InMemorySessionStore();
        var session = new TelegramLikeSession(identityAuth, store);
        await session.LoginAsync("a@b.com", "pw");

        await session.LogoutAsync();

        session.UserId.Should().BeNull();
        session.Username.Should().BeNull();
        (await store.GetSessionTokenAsync()).Should().BeNull();

        var token = await session.GetAccessTokenAsync();
        token.Should().BeNull();
        // Still only the one exchange from login — logout must not trigger another.
        await identityAuth.Received(1).ExchangeAsync("session-token-3", Arg.Any<CancellationToken>());
        // Logout must revoke the session server-side, not just clear it locally.
        await identityAuth.Received(1).LogoutAsync("session-token-3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenNoSessionStored_DoesNotCallRevoke()
    {
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        var session = new TelegramLikeSession(identityAuth, new InMemorySessionStore());

        await session.LogoutAsync();

        await identityAuth.DidNotReceive().LogoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ReflectsWhetherASessionTokenIsStored()
    {
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        identityAuth.LoginAsync("a@b.com", "pw", Arg.Any<CancellationToken>()).Returns("session-token-4");
        identityAuth.ExchangeAsync("session-token-4", Arg.Any<CancellationToken>())
            .Returns(ExchangeResult(Guid.NewGuid()));

        var store = new InMemorySessionStore();
        var session = new TelegramLikeSession(identityAuth, store);

        (await session.IsAuthenticatedAsync()).Should().BeFalse();

        await session.LoginAsync("a@b.com", "pw");
        (await session.IsAuthenticatedAsync()).Should().BeTrue();

        await session.LogoutAsync();
        (await session.IsAuthenticatedAsync()).Should().BeFalse();
    }
}
