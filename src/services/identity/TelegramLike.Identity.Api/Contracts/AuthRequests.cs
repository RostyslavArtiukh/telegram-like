namespace TelegramLike.Identity.Api.Contracts;

// UserId is the client-generated idempotency key. Empty/absent => the service mints
// one. A retried register reuses it so it isn't treated as a fresh "email taken".
public sealed record RegisterRequest(string Email, string Username, string DisplayName, string Password, Guid UserId = default);

public sealed record LoginRequest(string Email, string Password);

public sealed record TokenRequest(string SessionToken);
