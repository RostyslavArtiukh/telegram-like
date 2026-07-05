using TelegramLike.Client.Auth;

namespace TelegramLike.App.Services;

/// <summary>
/// Persists the opaque Identity session token in the platform keystore
/// (MAUI SecureStorage → Android Keystore), so the phone stays logged in across
/// launches. Registered on Android only: on unpackaged Windows SecureStorage
/// isn't available, so desktop keeps the SDK's in-memory store (login per launch).
/// </summary>
public sealed class SecureSessionStore : ISessionStore
{
    private const string Key = "telegramlike_session_token";

    public async Task<string?> GetSessionTokenAsync(CancellationToken ct = default)
        => await SecureStorage.Default.GetAsync(Key);

    public Task SetSessionTokenAsync(string? sessionToken, CancellationToken ct = default)
    {
        if (sessionToken is null)
        {
            SecureStorage.Default.Remove(Key);
            return Task.CompletedTask;
        }
        return SecureStorage.Default.SetAsync(Key, sessionToken);
    }
}
