using TelegramLike.Web.Services.NotificationsApi;

namespace TelegramLike.Web.Services.PresenceApi;

internal sealed class PresenceApiClient(HttpClient http) : IPresenceApi
{
    public async Task HeartbeatAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/presence/heartbeat");
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task GoOfflineAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/presence/offline");
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
