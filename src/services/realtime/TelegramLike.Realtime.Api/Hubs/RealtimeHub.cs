using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Api.Hubs;

/// <summary>
/// Push channel for external clients (MAUI/desktop via the TelegramLike.Client SDK).
/// Every connection is auto-joined to its per-user group (chat list / badge events);
/// clients join per-chat groups explicitly while a chat is open (message/typing/
/// reaction events). The hub never persists anything — consumers fan RabbitMQ
/// integration events into these groups.
/// </summary>
[Authorize]
public sealed class RealtimeHub(ChatMembershipCheck membership) : Hub
{
    public override async Task OnConnectedAsync()
    {
        // MapInboundClaims=false keeps the raw "sub" claim (userId) — the default
        // IUserIdProvider looks for ClaimTypes.NameIdentifier, which doesn't exist here.
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId));

        await base.OnConnectedAsync();
    }

    // Reject a non-member from subscribing to a chat's live events. The check answers from
    // what this replica already knows and otherwise asks Chats on the caller's behalf, so a
    // chat it has never observed is no longer waved through ([TL-127]).
    public async Task JoinChat(Guid chatId)
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            throw new HubException("You are not a member of this chat.");

        var mayJoin = await membership.MayJoinAsync(chatId, userId, AccessToken(), Context.ConnectionAborted);
        if (!mayJoin)
            throw new HubException("You are not a member of this chat.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Chat(chatId));
    }

    public Task LeaveChat(Guid chatId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Chat(chatId));

    // The token this connection authenticated with, forwarded so Chats answers as the user
    // rather than as this service. WebSocket clients can't set headers on the upgrade, so it
    // arrives as ?access_token= (the same place JwtBearerEvents reads it from); other
    // transports use the header.
    private string? AccessToken()
    {
        var http = Context.GetHttpContext();
        if (http is null) return null;

        var queryToken = http.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(queryToken)) return queryToken;

        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..]
            : null;
    }
}

internal static class RealtimeGroups
{
    public static string User(string userId) => $"user:{userId}";
    public static string User(Guid userId) => $"user:{userId}";
    public static string Chat(Guid chatId) => $"chat:{chatId}";
}
