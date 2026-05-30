using System.Net.Http.Json;
using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Web.Services.NotificationsApi;

internal sealed class NotificationsApiClient(HttpClient http) : INotificationsApi
{
    public async Task<NotificationFeedApiDto> GetFeedAsync(
        Guid userId,
        DateTime? beforeCreatedAt = null,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"pageSize={pageSize}", $"unreadOnly={unreadOnly.ToString().ToLowerInvariant()}" };
        if (beforeCreatedAt.HasValue)
            query.Add($"before={Uri.EscapeDataString(beforeCreatedAt.Value.ToString("o"))}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/notifications/?" + string.Join("&", query));
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<NotificationFeedApiDto>(ct)
               ?? new NotificationFeedApiDto([], null);
    }

    public async Task<long> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/notifications/unread-count");
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UnreadCountResponse>(ct);
        return payload?.Count ?? 0;
    }

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/notifications/{notificationId}/read");
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/notifications/read-all");
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkChatAsReadAsync(Guid userId, Guid chatId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/notifications/chats/{chatId}/read");
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record UnreadCountResponse(long Count);
}
