using System.Net;
using System.Net.Http.Json;

namespace TelegramLike.Web.Services.IdentityApi;

// Talks to the public Identity endpoints on a plain HttpClient (no access-token
// handler — these calls bootstrap authentication, so there's no token yet, and
// routing the exchange call through the token provider would be circular).
internal sealed class IdentityAuthApiClient(HttpClient http) : IIdentityAuthApi
{
    public async Task<Guid> RegisterAsync(
        string email, string username, string displayName, string password, CancellationToken ct = default)
    {
        using var resp = await http.PostAsJsonAsync("/auth/register",
            new { email, username, displayName, password }, ct);
        await EnsureOkAsync(resp, ct);
        var payload = await resp.Content.ReadFromJsonAsync<RegisterResponse>(ct);
        return payload?.UserId ?? throw new InvalidOperationException("Identity returned no user id.");
    }

    public async Task<string> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        using var resp = await http.PostAsJsonAsync("/auth/login", new { email, password }, ct);
        await EnsureOkAsync(resp, ct);
        var payload = await resp.Content.ReadFromJsonAsync<LoginResponse>(ct);
        return payload?.SessionToken ?? throw new InvalidOperationException("Identity returned no session token.");
    }

    public async Task<SessionExchangeResult?> ExchangeAsync(string sessionToken, CancellationToken ct = default)
    {
        using var resp = await http.PostAsJsonAsync("/auth/token", new { sessionToken }, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SessionExchangeResult>(ct);
    }

    // Identity returns { "error": "..." } with 400 for validation/business failures;
    // surface that message so the Razor pages can show it (mirrors the old in-process
    // ValidationException / InvalidOperationException handling).
    private static async Task EnsureOkAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            throw new InvalidOperationException(err?.Error ?? "Request failed.");
        }
        resp.EnsureSuccessStatusCode();
    }

    private sealed record RegisterResponse(Guid UserId);
    private sealed record LoginResponse(string SessionToken);
    private sealed record ErrorResponse(string Error);
}
