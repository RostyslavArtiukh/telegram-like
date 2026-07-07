using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TelegramLike.Client.Auth;

namespace TelegramLike.Client.Chats;

internal sealed class ChatsApiClient(HttpClient http, IAccessTokenProvider tokenProvider) : IChatsApi
{
    public async Task<IReadOnlyList<ChatSummary>> GetMyChatsAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, "/chats/my", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatSummary>>(ct) ?? [];
    }

    public async Task<ChatDetails?> GetChatByIdAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/chats/{chatId}", ct);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatDetails>(ct);
    }

    public async Task<IReadOnlyList<ChatMember>> GetChatMembersAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"/chats/{chatId}/members", ct);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatMember>>(ct) ?? [];
    }

    // Each create generates the chat id up front as the idempotency key (body +
    // Idempotency-Key header, so the resilience pipeline may safely retry). The server
    // reply is still the authoritative id — a direct-chat create returns the existing
    // chat's id when a chat between the pair already exists.
    public async Task<Guid> CreateDirectChatAsync(Guid userId, Guid peerUserId, CancellationToken ct = default)
    {
        var chatId = Guid.NewGuid();
        using var request = await NewRequestAsync(HttpMethod.Post, "/chats/direct", ct);
        request.Headers.Add("Idempotency-Key", chatId.ToString());
        request.Content = JsonContent.Create(new { chatId, peerUserId });
        return await SendCreate(request, ct);
    }

    public async Task<Guid> CreateGroupChatAsync(Guid userId, string name, CancellationToken ct = default)
    {
        var chatId = Guid.NewGuid();
        using var request = await NewRequestAsync(HttpMethod.Post, "/chats/group", ct);
        request.Headers.Add("Idempotency-Key", chatId.ToString());
        request.Content = JsonContent.Create(new { chatId, name });
        return await SendCreate(request, ct);
    }

    public async Task<Guid> CreateBroadcastChannelAsync(Guid userId, string name, CancellationToken ct = default)
    {
        var chatId = Guid.NewGuid();
        using var request = await NewRequestAsync(HttpMethod.Post, "/chats/broadcast", ct);
        request.Headers.Add("Idempotency-Key", chatId.ToString());
        request.Content = JsonContent.Create(new { chatId, name });
        return await SendCreate(request, ct);
    }

    public Task JoinChatAsync(Guid userId, Guid chatId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/join", content: null, ct);

    public Task LeaveChatAsync(Guid userId, Guid chatId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/leave", content: null, ct);

    public Task KickMemberAsync(Guid actorUserId, Guid chatId, Guid targetUserId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/members/{targetUserId}/kick", content: null, ct);

    public Task ChangeMemberRoleAsync(Guid actorUserId, Guid chatId, Guid targetUserId, MemberRole newRole, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/members/{targetUserId}/role",
            JsonContent.Create(new { newRole = newRole.ToString() }), ct);

    public Task TransferOwnershipAsync(Guid currentOwnerUserId, Guid chatId, Guid newOwnerUserId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/transfer-ownership",
            JsonContent.Create(new { newOwnerUserId }), ct);

    public Task RenameChatAsync(Guid actorUserId, Guid chatId, string newName, CancellationToken ct = default)
        => SendVoid(HttpMethod.Patch, $"/chats/{chatId}",
            JsonContent.Create(new { newName }), ct);

    public async Task<IReadOnlyList<Guid>> GetActiveRecipientsAsync(
        Guid actingUserId, Guid chatId, Guid excludeUserId, CancellationToken ct = default)
    {
        var members = await GetChatMembersAsync(actingUserId, chatId, ct);
        return members
            .Where(m => m.Status == MemberStatus.Active && m.UserId != excludeUserId)
            .Select(m => m.UserId)
            .ToList();
    }

    public async Task<ChatType?> GetChatTypeAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default)
    {
        var details = await GetChatByIdAsync(actingUserId, chatId, ct);
        return details?.Type;
    }

    public async Task<bool> IsModeratorAsync(Guid actingUserId, Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var members = await GetChatMembersAsync(actingUserId, chatId, ct);
        var me = members.FirstOrDefault(m => m.UserId == userId);
        return me is { Status: MemberStatus.Active, Role: MemberRole.Owner or MemberRole.Admin };
    }

    private async Task<Guid> SendCreate(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatCreatedPayload>(ct);
        return payload?.ChatId ?? throw new InvalidOperationException("Chats.Api returned no chat id.");
    }

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

    private sealed record ChatCreatedPayload(Guid ChatId);
}
