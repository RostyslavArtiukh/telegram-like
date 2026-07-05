using Microsoft.Extensions.Caching.Memory;
using TelegramLike.Client.Auth;
using TelegramLike.Client.Identity;

namespace TelegramLike.Web.Services.ServiceAuth;

/// <summary>
/// The Web BFF's <see cref="IAccessTokenProvider"/>: resolves the short-lived access
/// JWT for the current user by exchanging their opaque session token (from the auth
/// cookie) at the Identity service — the IdP. Cached in memory until shortly before
/// expiry. The SDK's typed clients call this per request.
///
/// Scoped on purpose: it must run inside the Blazor circuit scope where the auth
/// cookie / AuthenticationState is readable. A pooled DelegatingHandler cannot
/// read circuit-scoped auth state safely, which is why downstream HTTP clients
/// call this and attach the Bearer header themselves rather than relying on a handler.
/// </summary>
public sealed class ServiceTokenProvider(
    CurrentUserAccessor currentUser,
    IIdentityAuthApi identityAuth,
    IMemoryCache cache) : IAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var sessionToken = await currentUser.GetSessionTokenAsync();
        if (string.IsNullOrEmpty(sessionToken)) return null;

        var cacheKey = "svc-access-token:" + sessionToken;
        if (cache.TryGetValue<string>(cacheKey, out var cached) && cached is not null)
            return cached;

        var exchange = await identityAuth.ExchangeAsync(sessionToken, ct);
        if (exchange is null) return null;

        // Refresh slightly before the token actually expires to avoid edge-of-expiry 401s.
        var ttl = TimeSpan.FromSeconds(Math.Max(30, exchange.ExpiresInSeconds - 30));
        cache.Set(cacheKey, exchange.AccessToken, ttl);
        return exchange.AccessToken;
    }
}
