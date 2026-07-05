using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Messaging;

internal sealed class MessagingApiClient(HttpClient http, IAccessTokenProvider tokenProvider) : IMessagingApi
{
    public async Task<Guid> SendMessageAsync(
        Guid authorUserId,
        Guid chatId,
        string? text,
        IReadOnlyList<Guid> recipients,
        bool isBroadcast,
        IReadOnlyList<SendMessageAttachmentContract>? attachments = null,
        Guid? replyToMessageId = null,
        Guid? forwardOriginalMessageId = null,
        Guid? forwardOriginalChatId = null,
        CancellationToken ct = default)
    {
        // Client-generated id doubles as the idempotency key. The Idempotency-Key header
        // signals the resilience pipeline that this POST is safe to retry; the Messaging
        // service dedupes on the same id, so a retried send never duplicates the message.
        var messageId = Guid.NewGuid();

        using var request = await NewRequestAsync(HttpMethod.Post, "/messages/", ct);
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

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return messageId;
    }

    public async Task<MessageContract?> GetMessageByIdAsync(Guid userId, Guid messageId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/messages/{messageId}", ct);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessageContract>(ct);
    }

    public async Task<MessagePageContract> GetChatMessagesAsync(
        Guid userId,
        Guid chatId,
        DateTime? before = null,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"pageSize={pageSize}" };
        if (before.HasValue)
            query.Add($"before={Uri.EscapeDataString(before.Value.ToString("o"))}");

        using var request = await NewRequestAsync(HttpMethod.Get,
            $"/chats/{chatId}/messages?{string.Join("&", query)}", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessagePageContract>(ct)
               ?? new MessagePageContract([], null);
    }

    public Task AddReactionAsync(Guid userId, Guid messageId, EmojiContract emoji, bool actorIsPremium, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/reactions",
            JsonContent.Create(new { emoji = emoji.ToString(), actorIsPremium }), ct);

    public Task RemoveReactionAsync(Guid userId, Guid messageId, EmojiContract emoji, CancellationToken ct = default)
        => SendVoid(HttpMethod.Delete, $"/messages/{messageId}/reactions/{emoji}", content: null, ct);

    public Task RetractMessageAsync(Guid actorUserId, Guid messageId, bool actorIsModerator, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/retract",
            JsonContent.Create(new { actorIsModerator }), ct);

    public Task MarkAsReadAsync(Guid userId, Guid messageId, bool isBroadcast, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/read",
            JsonContent.Create(new { isBroadcast }), ct);

    public Task HideMessageAsync(Guid userId, Guid messageId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/hide", content: null, ct);

    private async Task SendVoid(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var request = await NewRequestAsync(method, url, ct);
        if (content is not null) request.Content = content;
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
