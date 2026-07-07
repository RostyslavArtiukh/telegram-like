namespace TelegramLike.Client.Identity;

/// <summary>
/// Public Identity (IdP) endpoints — no bearer token (the caller isn't
/// authenticated yet). Used by login/register flows and by the access-token
/// exchange that backs <see cref="Auth.IAccessTokenProvider"/> implementations.
/// </summary>
public interface IIdentityAuthApi
{
    Task<Guid> RegisterAsync(string email, string username, string displayName, string password, CancellationToken cancellationToken = default);
    Task<string> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<SessionExchangeResult?> ExchangeAsync(string sessionToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Authenticated Identity user queries — calls carry an Identity-issued access JWT.
/// </summary>
public interface IIdentityUsersApi
{
    Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> GetUsernamesByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
    Task<Guid?> GetUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

public sealed record IdentityUser(
    Guid Id, string Email, string Username, string DisplayName, string? AvatarUrl, bool IsPremium, DateTime CreatedAt);

public sealed record SessionExchangeResult(
    Guid UserId, string Username, string Email, string AccessToken, int ExpiresInSeconds);
