using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Client.Auth;
using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Client.Notifications;

internal sealed class NotificationsApiClient(HttpClient http, IAccessTokenProvider tokenProvider) : INotificationsApi
{
    public async Task<NotificationFeedApiDto> GetFeedAsync(
        Guid userId,
        DateTime? beforeCreatedAt = null,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"pageSize={pageSize}", $"unreadOnly={unreadOnly.ToString().ToLowerInvariant()}" };
        if (beforeCreatedAt.HasValue)
            query.Add($"before={Uri.EscapeDataString(beforeCreatedAt.Value.ToString("o"))}");

        using var request = await NewRequestAsync(HttpMethod.Get, "/notifications/?" + string.Join("&", query), cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<NotificationFeedApiDto>(cancellationToken)
               ?? new NotificationFeedApiDto([], null);
    }

    public async Task<long> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, "/notifications/unread-count", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UnreadCountResponse>(cancellationToken);
        return payload?.Count ?? 0;
    }

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"/notifications/{notificationId}/read", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, "/notifications/read-all", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkChatAsReadAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"/notifications/chats/{chatId}/read", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record UnreadCountResponse(long Count);
}
