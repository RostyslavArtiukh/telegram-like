using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Identity;

/// <summary>
/// Authenticated Identity user queries — calls carry an Identity-issued access JWT.
/// </summary>
public sealed class IdentityUsersApiClient(HttpClient http, IAccessTokenProvider tokenProvider)
{
    public async Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/users/{userId}", cancellationToken);
        using var resp = await http.SendAsync(request, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<IdentityUser>(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUsernamesByIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();

        using var request = await NewRequestAsync(HttpMethod.Post, "/users/by-ids", cancellationToken);
        request.Content = JsonContent.Create(userIds);
        using var resp = await http.SendAsync(request, cancellationToken);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Dictionary<Guid, string>>(cancellationToken) ?? new Dictionary<Guid, string>();
    }

    public async Task<Guid?> GetUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(
            HttpMethod.Get, $"/users/by-username?u={Uri.EscapeDataString(username)}", cancellationToken);
        using var resp = await http.SendAsync(request, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<UserIdResponse>(cancellationToken);
        return payload?.UserId;
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record UserIdResponse(Guid UserId);
}
