using TelegramLike.Client.Identity;

namespace TelegramLike.Client.Auth;

/// <summary>
/// Auth session for standalone (non-browser) clients: log in once to obtain the
/// opaque Identity session token, then exchange it for short-lived access JWTs on
/// demand, cached until shortly before expiry. Mirrors what the Web BFF does with
/// its cookie + ServiceTokenProvider, minus the cookie.
///
/// Registered as a singleton by <c>AddTelegramLikeClient</c> — one user session
/// per app process, which is the desktop/mobile model.
/// </summary>
public sealed class TelegramLikeSession(IIdentityAuthApi identityAuth, ISessionStore store)
    : IAccessTokenProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Read on the lock-free fast path from multiple threads (HTTP clients, the SignalR
    // access-token callback, the presence heartbeat timer). `volatile` on the string ref
    // and Volatile.Read/Write on the expiry (a 64-bit value can't be `volatile`) prevent
    // a torn read that could hand out an expired token or trigger a spurious refresh.
    private volatile string? _accessToken;
    private long _expiresAtUtcTicks;

    // Published as a single volatile reference to an immutable record so a concurrent
    // reader (UI, heartbeat timer) never sees a torn Guid?/string pair — the previous
    // plain auto-properties were written under the gate but read lock-free.
    private volatile UserIdentity? _identity;

    /// <summary>Identity of the logged-in user, populated by the first successful token exchange.</summary>
    public Guid? UserId => _identity?.UserId;
    public string? Username => _identity?.Username;

    private sealed record UserIdentity(Guid UserId, string Username);

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
        => await store.GetSessionTokenAsync(ct) is not null;

    public async Task RegisterAsync(
        string email, string username, string displayName, string password, CancellationToken ct = default)
        => await identityAuth.RegisterAsync(email, username, displayName, password, ct);

    public async Task LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var sessionToken = await identityAuth.LoginAsync(email, password, ct);
        await store.SetSessionTokenAsync(sessionToken, ct);
        InvalidateAccessToken();

        // Exchange eagerly so a bad session fails at login, and UserId is known right away.
        if (await GetAccessTokenAsync(ct) is null)
            throw new InvalidOperationException("Login succeeded but the token exchange failed.");
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await store.SetSessionTokenAsync(null, ct);
        InvalidateAccessToken();
        _identity = null;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // Fast path outside the lock; the margin mirrors the Web BFF's refresh-before-expiry.
        if (_accessToken is not null && DateTimeOffset.UtcNow.UtcTicks < Volatile.Read(ref _expiresAtUtcTicks))
            return _accessToken;

        await _gate.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow.UtcTicks < Volatile.Read(ref _expiresAtUtcTicks))
                return _accessToken;

            var sessionToken = await store.GetSessionTokenAsync(ct);
            if (sessionToken is null) return null;

            var exchange = await identityAuth.ExchangeAsync(sessionToken, ct);
            if (exchange is null)
            {
                // Session expired or revoked server-side — drop it so the app re-prompts login.
                await store.SetSessionTokenAsync(null, ct);
                return null;
            }

            _identity = new UserIdentity(exchange.UserId, exchange.Username);
            Volatile.Write(ref _expiresAtUtcTicks,
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, exchange.ExpiresInSeconds - 30)).UtcTicks);
            _accessToken = exchange.AccessToken;
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void InvalidateAccessToken()
    {
        _accessToken = null;
        Volatile.Write(ref _expiresAtUtcTicks, 0);
    }
}
