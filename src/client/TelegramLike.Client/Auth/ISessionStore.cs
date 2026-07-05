namespace TelegramLike.Client.Auth;

/// <summary>
/// Where the durable credential — the opaque Identity session token — lives between
/// runs. In-memory by default; a MAUI app swaps in SecureStorage, a console app a file.
/// </summary>
public interface ISessionStore
{
    Task<string?> GetSessionTokenAsync(CancellationToken ct = default);
    Task SetSessionTokenAsync(string? sessionToken, CancellationToken ct = default);
}

public sealed class InMemorySessionStore : ISessionStore
{
    private string? _sessionToken;

    public Task<string?> GetSessionTokenAsync(CancellationToken ct = default)
        => Task.FromResult(_sessionToken);

    public Task SetSessionTokenAsync(string? sessionToken, CancellationToken ct = default)
    {
        _sessionToken = sessionToken;
        return Task.CompletedTask;
    }
}
