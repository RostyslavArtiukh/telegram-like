namespace TelegramLike.Client.Identity;

/// <summary>
/// Public Identity (IdP) endpoints — no bearer token (the caller isn't
/// authenticated yet). Used by login/register flows and by the access-token
/// exchange that backs <see cref="Auth.IAccessTokenProvider"/> implementations.
/// </summary>
public interface IIdentityAuthApi
{
    Task<Guid> RegisterAsync(string email, string username, string displayName, string password, CancellationToken ct = default);
    Task<string> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<SessionExchangeResult?> ExchangeAsync(string sessionToken, CancellationToken ct = default);
}

/// <summary>
/// Authenticated Identity user queries — calls carry an Identity-issued access JWT.
/// </summary>
public interface IIdentityUsersApi
{
    Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, string>> GetUsernamesByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<Guid?> GetUserIdByUsernameAsync(string username, CancellationToken ct = default);
}

public sealed record IdentityUser(
    Guid Id, string Email, string Username, string DisplayName, string? AvatarUrl, bool IsPremium, DateTime CreatedAt);

public sealed record SessionExchangeResult(
    Guid UserId, string Username, string Email, string AccessToken, int ExpiresInSeconds);
