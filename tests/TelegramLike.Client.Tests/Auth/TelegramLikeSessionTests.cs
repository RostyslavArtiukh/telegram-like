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
    public async Task LoginAsync_stores_session_token_and_eagerly_exchanges()
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
    public async Task LoginAsync_when_exchange_fails_throws_and_clears_stored_token()
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
    public async Task GetAccessTokenAsync_with_no_session_token_returns_null_without_calling_exchange()
    {
        var identityAuth = Substitute.For<IIdentityAuthApi>();
        var session = new TelegramLikeSession(identityAuth, new InMemorySessionStore());

        var token = await session.GetAccessTokenAsync();

        token.Should().BeNull();
        await identityAuth.DidNotReceive().ExchangeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_when_exchange_returns_null_clears_stored_token_and_returns_null()
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
    public async Task GetAccessTokenAsync_caches_across_concurrent_calls_and_exchanges_only_once()
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
    public async Task LogoutAsync_clears_stored_token_and_cached_access_token()
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
    }

    [Fact]
    public async Task IsAuthenticatedAsync_reflects_whether_a_session_token_is_stored()
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
