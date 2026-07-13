namespace TelegramLike.Identity.Application.Security;

/// <summary>
/// Mints a short-lived service access token (JWT) for a user. Identity is the IdP:
/// all downstream services validate these tokens (issuer = telegramlike-identity).
/// The Web BFF obtains one by exchanging a session token, then forwards it as a
/// Bearer header on downstream calls.
/// </summary>
public interface IAccessTokenIssuer
{
    // isPremium is embedded as a signed claim so downstream services (e.g. Messaging's
    // reaction limit) can read premium status from the validated token instead of
    // trusting a spoofable client-supplied flag. Premium changes take effect on the
    // next token refresh (≤ the access-token lifetime).
    AccessToken IssueForUser(Guid userId, bool isPremium);
}

public sealed record AccessToken(string Token, int ExpiresInSeconds);
