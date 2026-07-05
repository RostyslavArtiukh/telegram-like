using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Presence;

internal sealed class PresenceApiClient(HttpClient http, IAccessTokenProvider tokenProvider) : IPresenceApi
{
    public async Task HeartbeatAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, "/presence/heartbeat", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task GoOfflineAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, "/presence/offline", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task StartTypingAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"/presence/typing/{chatId}/start", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopTypingAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"/presence/typing/{chatId}/stop", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<Guid>> GetTypingUsersAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/presence/typing/{chatId}", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TypingUsersPayload>(ct);
        return payload?.UserIds ?? [];
    }

    public async Task<UserPresenceSummary?> GetUserPresenceAsync(
        Guid actorUserId, Guid targetUserId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/presence/{targetUserId}", ct);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PresencePayload>(ct);
        if (payload is null) return null;

        return new UserPresenceSummary(payload.UserId, payload.Status == 1, payload.LastSeenAt);
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetBatchPresenceAsync(
        Guid actorUserId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, bool>();

        using var request = await NewRequestAsync(HttpMethod.Post, "/presence/batch", ct);
        request.Content = JsonContent.Create(userIds);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>(ct);
        return result ?? new Dictionary<Guid, bool>();
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record TypingUsersPayload(
        [property: JsonPropertyName("chatId")] Guid ChatId,
        [property: JsonPropertyName("userIds")] List<Guid> UserIds);

    // Presence service returns Status as integer (0=Offline, 1=Online) per OnlineStatus enum.
    private sealed record PresencePayload(
        [property: JsonPropertyName("userId")] Guid UserId,
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("lastSeenAt")] DateTime? LastSeenAt,
        [property: JsonPropertyName("hideLastSeen")] bool HideLastSeen);
}
