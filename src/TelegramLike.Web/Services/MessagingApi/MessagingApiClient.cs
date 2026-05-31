using System.Net;
using System.Net.Http.Json;
using TelegramLike.Web.Services.ServiceAuth;

namespace TelegramLike.Web.Services.MessagingApi;

internal sealed class MessagingApiClient(HttpClient http) : IMessagingApi
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
        using var request = NewRequest(HttpMethod.Post, "/messages/", authorUserId);
        request.Content = JsonContent.Create(new
        {
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
        var payload = await response.Content.ReadFromJsonAsync<MessageCreatedPayload>(ct);
        return payload?.MessageId ?? throw new InvalidOperationException("Messaging.Api returned no message id.");
    }

    public async Task<MessageContract?> GetMessageByIdAsync(Guid userId, Guid messageId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, $"/messages/{messageId}", userId);
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

        using var request = NewRequest(HttpMethod.Get,
            $"/chats/{chatId}/messages?{string.Join("&", query)}", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessagePageContract>(ct)
               ?? new MessagePageContract([], null);
    }

    public Task AddReactionAsync(Guid userId, Guid messageId, EmojiContract emoji, bool actorIsPremium, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/reactions", userId,
            JsonContent.Create(new { emoji = emoji.ToString(), actorIsPremium }), ct);

    public Task RemoveReactionAsync(Guid userId, Guid messageId, EmojiContract emoji, CancellationToken ct = default)
        => SendVoid(HttpMethod.Delete, $"/messages/{messageId}/reactions/{emoji}", userId, content: null, ct);

    public Task RetractMessageAsync(Guid actorUserId, Guid messageId, bool actorIsModerator, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/retract", actorUserId,
            JsonContent.Create(new { actorIsModerator }), ct);

    public Task MarkAsReadAsync(Guid userId, Guid messageId, bool isBroadcast, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/read", userId,
            JsonContent.Create(new { isBroadcast }), ct);

    public Task HideMessageAsync(Guid userId, Guid messageId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/messages/{messageId}/hide", userId, content: null, ct);

    private async Task SendVoid(HttpMethod method, string url, Guid userId, HttpContent? content, CancellationToken ct)
    {
        using var request = NewRequest(method, url, userId);
        if (content is not null) request.Content = content;
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string url, Guid userId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Options.Set(ServiceAuthHandler.UserIdKey, userId);
        return request;
    }

    private sealed record MessageCreatedPayload(Guid MessageId);
}
