using MediatR;

namespace TelegramLike.Identity.Application.Commands.EndSession;

/// <summary>
/// Logout: revoke an opaque session token by deleting it from the session store.
/// Once gone, the token can no longer be exchanged for access JWTs. Idempotent —
/// an unknown/already-expired token is a no-op, so a retried or racing logout is safe.
/// </summary>
public sealed record EndSessionCommand(string SessionToken) : IRequest;
