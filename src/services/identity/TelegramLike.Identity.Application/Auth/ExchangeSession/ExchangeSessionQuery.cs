using MediatR;

namespace TelegramLike.Identity.Application.Auth.ExchangeSession;

/// <summary>
/// Exchanges an opaque session token (issued at login, stored in Redis) for a
/// short-lived access JWT plus the user's identity claims. The Web BFF calls this
/// at /auth/signin (to set the cookie) and again whenever its cached access token
/// expires. Returns <c>null</c> when the session token is unknown/expired.
/// </summary>
public sealed record ExchangeSessionQuery(string SessionToken) : IRequest<SessionExchangeDto?>;

public sealed record SessionExchangeDto(
    Guid UserId,
    string Username,
    string Email,
    string AccessToken,
    int ExpiresInSeconds);
