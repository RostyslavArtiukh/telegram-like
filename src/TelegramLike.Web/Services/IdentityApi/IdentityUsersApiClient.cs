using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Web.Services.ServiceAuth;

namespace TelegramLike.Web.Services.IdentityApi;

internal sealed class IdentityUsersApiClient(HttpClient http, ServiceTokenProvider tokenProvider) : IIdentityUsersApi
{
    public async Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/users/{userId}", ct);
        using var resp = await http.SendAsync(request, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<IdentityUser>(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUsernamesByIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();

        using var request = await NewRequestAsync(HttpMethod.Post, "/users/by-ids", ct);
        request.Content = JsonContent.Create(userIds);
        using var resp = await http.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Dictionary<Guid, string>>(ct) ?? new Dictionary<Guid, string>();
    }

    public async Task<Guid?> GetUserIdByUsernameAsync(string username, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(
            HttpMethod.Get, $"/users/by-username?u={Uri.EscapeDataString(username)}", ct);
        using var resp = await http.SendAsync(request, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<UserIdResponse>(ct);
        return payload?.UserId;
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record UserIdResponse(Guid UserId);
}
