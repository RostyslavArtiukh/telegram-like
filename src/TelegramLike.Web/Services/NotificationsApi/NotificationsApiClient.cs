using System.Net.Http.Json;
using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Web.Services.NotificationsApi;

internal sealed class NotificationsApiClient(HttpClient http) : INotificationsApi
{
    public async Task<NotificationFeedApiDto> GetFeedAsync(
        DateTime? beforeCreatedAt = null,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"pageSize={pageSize}", $"unreadOnly={unreadOnly.ToString().ToLowerInvariant()}" };
        if (beforeCreatedAt.HasValue)
            query.Add($"before={Uri.EscapeDataString(beforeCreatedAt.Value.ToString("o"))}");

        var url = "/notifications/?" + string.Join("&", query);
        var feed = await http.GetFromJsonAsync<NotificationFeedApiDto>(url, ct);
        return feed ?? new NotificationFeedApiDto([], null);
    }

    public async Task<long> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<UnreadCountResponse>("/notifications/unread-count", ct);
        return response?.Count ?? 0;
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"/notifications/{notificationId}/read", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("/notifications/read-all", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record UnreadCountResponse(long Count);
}
