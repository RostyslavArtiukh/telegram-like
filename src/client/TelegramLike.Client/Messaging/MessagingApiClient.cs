using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Messaging;

public sealed class MessagingApiClient(HttpClient http, IAccessTokenProvider tokenProvider)
{
    public async Task<Guid> SendMessageAsync(
        Guid authorUserId,
        Guid chatId,
        string? text,
        IReadOnlyList<Guid> recipients,
        bool isBroadcast,
        IReadOnlyList<OutgoingAttachment>? attachments = null,
        Guid? replyToMessageId = null,
        Guid? forwardOriginalMessageId = null,
        Guid? forwardOriginalChatId = null,
        CancellationToken cancellationToken = default)
    {
        // Client-generated id doubles as the duplicate-protection key. The Idempotency-Key header
        // signals the resilience pipeline that this POST is safe to retry; the Messaging
        // service dedupes on the same id, so a retried send never duplicates the message.
        var messageId = Guid.NewGuid();

        using var request = await NewRequestAsync(HttpMethod.Post, "/messages/", cancellationToken);
        request.Headers.Add("Idempotency-Key", messageId.ToString());
        request.Content = JsonContent.Create(new
        {
            messageId,
            chatId,
            text,
            recipients,
            isBroadcast,
            attachments,
            replyToMessageId,
            forwardOriginalMessageId,
            forwardOriginalChatId
        });

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return messageId;
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/messages/{messageId}", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatMessage>(cancellationToken);
    }

    public async Task<ChatMessagePage> GetChatMessagesAsync(
        Guid userId,
        Guid chatId,
        DateTime? before = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"pageSize={pageSize}" };
        if (before.HasValue)
            query.Add($"before={Uri.EscapeDataString(before.Value.ToString("o"))}");

        using var request = await NewRequestAsync(HttpMethod.Get,
            $"/chats/{chatId}/messages?{string.Join("&", query)}", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatMessagePage>(cancellationToken)
               ?? new ChatMessagePage([], null);
    }

    public Task AddReactionAsync(Guid userId, Guid messageId, ReactionEmoji emoji, bool userIsPremium, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/reactions",
            JsonContent.Create(new { emoji = emoji.ToString(), userIsPremium }), cancellationToken);

    public Task RemoveReactionAsync(Guid userId, Guid messageId, ReactionEmoji emoji, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Delete, $"/messages/{messageId}/reactions/{emoji}", content: null, cancellationToken);

    public Task RetractMessageAsync(Guid retractedByUserId, Guid messageId, bool retractedByModerator, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/retract",
            JsonContent.Create(new { retractedByModerator }), cancellationToken);

    public Task MarkAsReadAsync(Guid userId, Guid messageId, bool isBroadcast, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/read",
            JsonContent.Create(new { isBroadcast }), cancellationToken);

    public Task HideMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/hide", content: null, cancellationToken);

    private async Task SendVoid(HttpMethod method, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = await NewRequestAsync(method, url, cancellationToken);
        if (content is not null) request.Content = content;
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
}
