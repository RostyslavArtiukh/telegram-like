namespace TelegramLike.Identity.Api.Contracts;

public sealed record RegisterRequest(string Email, string Username, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record TokenRequest(string SessionToken);
