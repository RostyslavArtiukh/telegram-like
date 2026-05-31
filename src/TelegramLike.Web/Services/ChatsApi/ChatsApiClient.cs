using System.Net;
using System.Net.Http.Json;
using TelegramLike.Web.Services.ServiceAuth;

namespace TelegramLike.Web.Services.ChatsApi;

internal sealed class ChatsApiClient(HttpClient http) : IChatsApi
{
    public async Task<IReadOnlyList<ChatSummaryContract>> GetMyChatsAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, "/chats/my", userId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatSummaryContract>>(ct) ?? [];
    }

    public async Task<ChatDetailsContract?> GetChatByIdAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, $"/chats/{chatId}", actingUserId);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatDetailsContract>(ct);
    }

    public async Task<IReadOnlyList<ChatMemberContract>> GetChatMembersAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, $"/chats/{chatId}/members", actingUserId);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ChatMemberContract>>(ct) ?? [];
    }

    public async Task<Guid> CreateDirectChatAsync(Guid userId, Guid peerUserId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "/chats/direct", userId);
        request.Content = JsonContent.Create(new { peerUserId });
        return await SendCreate(request, ct);
    }

    public async Task<Guid> CreateGroupChatAsync(Guid userId, string name, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "/chats/group", userId);
        request.Content = JsonContent.Create(new { name });
        return await SendCreate(request, ct);
    }

    public async Task<Guid> CreateBroadcastChannelAsync(Guid userId, string name, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Post, "/chats/broadcast", userId);
        request.Content = JsonContent.Create(new { name });
        return await SendCreate(request, ct);
    }

    public Task JoinChatAsync(Guid userId, Guid chatId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/join", userId, content: null, ct);

    public Task LeaveChatAsync(Guid userId, Guid chatId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/leave", userId, content: null, ct);

    public Task KickMemberAsync(Guid actorUserId, Guid chatId, Guid targetUserId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/members/{targetUserId}/kick", actorUserId, content: null, ct);

    public Task ChangeMemberRoleAsync(Guid actorUserId, Guid chatId, Guid targetUserId, MemberRoleContract newRole, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/members/{targetUserId}/role", actorUserId,
            JsonContent.Create(new { newRole = newRole.ToString() }), ct);

    public Task TransferOwnershipAsync(Guid currentOwnerUserId, Guid chatId, Guid newOwnerUserId, CancellationToken ct = default)
        => SendVoid(HttpMethod.Post, $"/chats/{chatId}/transfer-ownership", currentOwnerUserId,
            JsonContent.Create(new { newOwnerUserId }), ct);

    public Task RenameChatAsync(Guid actorUserId, Guid chatId, string newName, CancellationToken ct = default)
        => SendVoid(HttpMethod.Patch, $"/chats/{chatId}", actorUserId,
            JsonContent.Create(new { newName }), ct);

    public async Task<IReadOnlyList<Guid>> GetActiveRecipientsAsync(
        Guid actingUserId, Guid chatId, Guid excludeUserId, CancellationToken ct = default)
    {
        var members = await GetChatMembersAsync(actingUserId, chatId, ct);
        return members
            .Where(m => m.Status == MemberStatusContract.Active && m.UserId != excludeUserId)
            .Select(m => m.UserId)
            .ToList();
    }

    public async Task<ChatTypeContract?> GetChatTypeAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default)
    {
        var details = await GetChatByIdAsync(actingUserId, chatId, ct);
        return details?.Type;
    }

    public async Task<bool> IsModeratorAsync(Guid actingUserId, Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var members = await GetChatMembersAsync(actingUserId, chatId, ct);
        var me = members.FirstOrDefault(m => m.UserId == userId);
        return me is { Status: MemberStatusContract.Active, Role: MemberRoleContract.Owner or MemberRoleContract.Admin };
    }

    private async Task<Guid> SendCreate(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatCreatedPayload>(ct);
        return payload?.ChatId ?? throw new InvalidOperationException("Chats.Api returned no chat id.");
    }

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

    private sealed record ChatCreatedPayload(Guid ChatId);
}
