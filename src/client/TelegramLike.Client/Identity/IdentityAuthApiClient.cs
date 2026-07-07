using System.Net;
using System.Net.Http.Json;

namespace TelegramLike.Client.Identity;

// Talks to the public Identity endpoints on a plain HttpClient (no access-token
// handler — these calls bootstrap authentication, so there's no token yet, and
// routing the exchange call through the token provider would be circular).
internal sealed class IdentityAuthApiClient(HttpClient http) : IIdentityAuthApi
{
    public async Task<Guid> RegisterAsync(
        string email, string username, string displayName, string password, CancellationToken cancellationToken = default)
    {
        // Client-generated id doubles as the idempotency key: the Idempotency-Key header
        // lets the resilience pipeline retry this POST, and Identity returns the same id
        // for a retry instead of a spurious "email already taken".
        var userId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new { userId, email, username, displayName, password })
        };
        request.Headers.Add("Idempotency-Key", userId.ToString());

        using var resp = await http.SendAsync(request, cancellationToken);
        await EnsureOkAsync(resp, cancellationToken);
        return userId;
    }

    public async Task<string> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var resp = await http.PostAsJsonAsync("/auth/login", new { email, password }, cancellationToken);
        await EnsureOkAsync(resp, cancellationToken);
        var payload = await resp.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        return payload?.SessionToken ?? throw new InvalidOperationException("Identity returned no session token.");
    }

    public async Task<SessionExchangeResult?> ExchangeAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        using var resp = await http.PostAsJsonAsync("/auth/token", new { sessionToken }, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SessionExchangeResult>(cancellationToken);
    }

    // Identity returns { "error": "..." } with 400 for validation/business failures;
    // surface that message so UI layers can show it.
    private static async Task EnsureOkAsync(HttpResponseMessage resp, CancellationToken cancellationToken)
    {
        if (resp.IsSuccessStatusCode) return;
        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
            throw new InvalidOperationException(err?.Error ?? "Request failed.");
        }
        resp.EnsureSuccessStatusCode();
    }

    private sealed record LoginResponse(string SessionToken);
    private sealed record ErrorResponse(string Error);
}
