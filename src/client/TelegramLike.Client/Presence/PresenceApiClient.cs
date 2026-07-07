using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Presence;

internal sealed class PresenceApiClient(HttpClient http, IAccessTokenProvider tokenProvider) : IPresenceApi
{
    public async Task HeartbeatAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, "/presence/heartbeat", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task GoOfflineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, "/presence/offline", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task StartTypingAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"/presence/typing/{chatId}/start", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopTypingAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"/presence/typing/{chatId}/stop", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<Guid>> GetTypingUsersAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/presence/typing/{chatId}", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TypingUsersPayload>(cancellationToken);
        return payload?.UserIds ?? [];
    }

    public async Task<UserPresenceSummary?> GetUserPresenceAsync(
        Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/presence/{targetUserId}", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PresencePayload>(cancellationToken);
        if (payload is null) return null;

        // Honor the privacy flag client-side too: never surface a last-seen timestamp
        // when the user hid it, even if the service included one on the wire.
        var lastSeenAt = payload.HideLastSeen ? null : payload.LastSeenAt;
        return new UserPresenceSummary(payload.UserId, payload.Status == 1, lastSeenAt);
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetBatchPresenceAsync(
        Guid actorUserId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, bool>();

        using var request = await NewRequestAsync(HttpMethod.Post, "/presence/batch", cancellationToken);
        request.Content = JsonContent.Create(userIds);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>(cancellationToken);
        return result ?? new Dictionary<Guid, bool>();
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
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
