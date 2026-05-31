using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TelegramLike.Web.Services.ServiceAuth;

namespace TelegramLike.Web.Services.PresenceApi;

internal sealed class PresenceApiClient(HttpClient http) : IPresenceApi
{
    public async Task HeartbeatAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "/presence/heartbeat", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task GoOfflineAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "/presence/offline", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task StartTypingAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, $"/presence/typing/{chatId}/start", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopTypingAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, $"/presence/typing/{chatId}/stop", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<Guid>> GetTypingUsersAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, $"/presence/typing/{chatId}", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TypingUsersPayload>(ct);
        return payload?.UserIds ?? [];
    }

    public async Task<UserPresenceSummary?> GetUserPresenceAsync(
        Guid actorUserId, Guid targetUserId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, $"/presence/{targetUserId}", actorUserId);
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

        using var request = NewRequest(HttpMethod.Post, "/presence/batch", actorUserId);
        request.Content = JsonContent.Create(userIds);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>(ct);
        return result ?? new Dictionary<Guid, bool>();
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string url, Guid userId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);
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
