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
public sealed class RealtimeHub(ChatMembershipTracker membership) : Hub
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

    // Reject a non-member from subscribing to a chat's live events. The membership
    // tracker is event-sourced and ephemeral, so it fails closed only for chats it has
    // actually observed; an unknown chat (e.g. just after a restart, before events
    // flow) stays fail-open to avoid locking legitimate members out.
    public Task JoinChat(Guid chatId)
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var userId)
            && membership.IsKnownChat(chatId)
            && !membership.IsMember(chatId, userId))
        {
            throw new HubException("You are not a member of this chat.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Chat(chatId));
    }

    public Task LeaveChat(Guid chatId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Chat(chatId));
}

internal static class RealtimeGroups
{
    public static string User(string userId) => $"user:{userId}";
    public static string User(Guid userId) => $"user:{userId}";
    public static string Chat(Guid chatId) => $"chat:{chatId}";
}
