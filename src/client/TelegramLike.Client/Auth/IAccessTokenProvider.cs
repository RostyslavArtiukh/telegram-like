namespace TelegramLike.Client.Auth;

/// <summary>
/// Resolves the current user's short-lived access JWT for downstream service calls.
/// Typed API clients call this per request and attach the Bearer header themselves
/// (never via a DelegatingHandler — handlers are pooled outside the host's user
/// scope and could leak one user's token to another in server-side hosts).
///
/// Hosts choose the implementation: the Web BFF adapts its cookie/circuit-scoped
/// token exchange; standalone apps (MAUI, console) use <see cref="TelegramLikeSession"/>.
/// </summary>
public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}
