using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TelegramLike.Realtime.Api.Hubs;

/// <summary>
/// Push channel for external clients (MAUI/desktop via the TelegramLike.Client SDK).
/// Every connection is auto-joined to its per-user group (chat list / badge events);
/// clients join per-chat groups explicitly while a chat is open (message/typing/
/// reaction events). The hub never persists anything — consumers fan RabbitMQ
/// integration events into these groups.
/// </summary>
[Authorize]
public sealed class RealtimeHub : Hub
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

    // Membership is not re-validated here (same trust model as Presence.StartTyping:
    // the caller holds an Identity-issued JWT). A non-member could subscribe to a
    // chat's events — acceptable for now, tracked with the messaging fail-open.
    public Task JoinChat(Guid chatId)
        => Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Chat(chatId));

    public Task LeaveChat(Guid chatId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Chat(chatId));
}

internal static class RealtimeGroups
{
    public static string User(string userId) => $"user:{userId}";
    public static string User(Guid userId) => $"user:{userId}";
    public static string Chat(Guid chatId) => $"chat:{chatId}";
}
