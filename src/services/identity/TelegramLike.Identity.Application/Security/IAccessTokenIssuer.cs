namespace TelegramLike.Identity.Application.Security;

/// <summary>
/// Mints a short-lived service access token (JWT) for a user. Identity is the IdP:
/// all downstream services validate these tokens (issuer = telegramlike-identity).
/// The Web BFF obtains one by exchanging a session token, then forwards it as a
/// Bearer header on downstream calls.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken IssueForUser(Guid userId);
}

public sealed record AccessToken(string Token, int ExpiresInSeconds);
