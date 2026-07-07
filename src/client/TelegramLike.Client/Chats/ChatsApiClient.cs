using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Chats;

internal sealed class ChatsApiClient(HttpClient http, IAccessTokenProvider tokenProvider) : IChatsApi
{
    public async Task<IReadOnlyList<ChatSummary>> GetMyChatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, "/chats/my", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatSummary>>(cancellationToken) ?? [];
    }

    public async Task<ChatDetails?> GetChatByIdAsync(Guid actingUserId, Guid chatId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/chats/{chatId}", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatDetails>(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMember>> GetChatMembersAsync(Guid actingUserId, Guid chatId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/chats/{chatId}/members", cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatMember>>(cancellationToken) ?? [];
    }

    // Each create generates the chat id up front as the idempotency key (body +
    // Idempotency-Key header, so the resilience pipeline may safely retry). The server
    // reply is still the authoritative id — a direct-chat create returns the existing
    // chat's id when a chat between the pair already exists.
    public async Task<Guid> CreateDirectChatAsync(Guid userId, Guid peerUserId, CancellationToken cancellationToken = default)
    {
        var chatId = Guid.NewGuid();
        using var request = await NewRequestAsync(HttpMethod.Post, "/chats/direct", cancellationToken);
        request.Headers.Add("Idempotency-Key", chatId.ToString());
        request.Content = JsonContent.Create(new { chatId, peerUserId });
        return await SendCreate(request, cancellationToken);
    }

    public async Task<Guid> CreateGroupChatAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        var chatId = Guid.NewGuid();
        using var request = await NewRequestAsync(HttpMethod.Post, "/chats/group", cancellationToken);
        request.Headers.Add("Idempotency-Key", chatId.ToString());
        request.Content = JsonContent.Create(new { chatId, name });
        return await SendCreate(request, cancellationToken);
    }

    public async Task<Guid> CreateBroadcastChannelAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        var chatId = Guid.NewGuid();
        using var request = await NewRequestAsync(HttpMethod.Post, "/chats/broadcast", cancellationToken);
        request.Headers.Add("Idempotency-Key", chatId.ToString());
        request.Content = JsonContent.Create(new { chatId, name });
        return await SendCreate(request, cancellationToken);
    }

    public Task JoinChatAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/join", content: null, cancellationToken);

    public Task LeaveChatAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/leave", content: null, cancellationToken);

    public Task KickMemberAsync(Guid actorUserId, Guid chatId, Guid targetUserId, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/members/{targetUserId}/kick", content: null, cancellationToken);

    public Task ChangeMemberRoleAsync(Guid actorUserId, Guid chatId, Guid targetUserId, MemberRole newRole, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/members/{targetUserId}/role",
            JsonContent.Create(new { newRole = newRole.ToString() }), cancellationToken);

    public Task TransferOwnershipAsync(Guid currentOwnerUserId, Guid chatId, Guid newOwnerUserId, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/transfer-ownership",
            JsonContent.Create(new { newOwnerUserId }), cancellationToken);

    public Task RenameChatAsync(Guid actorUserId, Guid chatId, string newName, CancellationToken cancellationToken = default)
        => SendVoid(HttpMethod.Patch, $"/chats/{chatId}",
            JsonContent.Create(new { newName }), cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetActiveRecipientsAsync(
        Guid actingUserId, Guid chatId, Guid excludeUserId, CancellationToken cancellationToken = default)
    {
        var members = await GetChatMembersAsync(actingUserId, chatId, cancellationToken);
        return members
            .Where(m => m.Status == MemberStatus.Active && m.UserId != excludeUserId)
            .Select(m => m.UserId)
            .ToList();
    }

    public async Task<ChatType?> GetChatTypeAsync(Guid actingUserId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var details = await GetChatByIdAsync(actingUserId, chatId, cancellationToken);
        return details?.Type;
    }

    public async Task<bool> IsModeratorAsync(Guid actingUserId, Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        var members = await GetChatMembersAsync(actingUserId, chatId, cancellationToken);
        var me = members.FirstOrDefault(m => m.UserId == userId);
        return me is { Status: MemberStatus.Active, Role: MemberRole.Owner or MemberRole.Admin };
    }

    private async Task<Guid> SendCreate(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatCreatedPayload>(cancellationToken);
        return payload?.ChatId ?? throw new InvalidOperationException("Chats.Api returned no chat id.");
    }

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

    private sealed record ChatCreatedPayload(Guid ChatId);
}
