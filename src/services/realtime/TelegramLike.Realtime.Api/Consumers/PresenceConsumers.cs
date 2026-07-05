using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TelegramLike.Contracts.Presence;
using TelegramLike.Contracts.Realtime;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Api.Consumers;

internal sealed class UserTypingConsumer(IHubContext<RealtimeHub> hub) : IConsumer<UserTypingIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserTypingIntegrationEvent> context)
    {
        var e = context.Message;
        return hub.Clients.Group(RealtimeGroups.Chat(e.ChatId)).SendAsync(
            RealtimeEventNames.UserTyping,
            new UserTypingPush(e.ChatId, e.UserId),
            context.CancellationToken);
    }
}

// Presence changes go to every connected client — mirrors the Web BFF, where any
// open chat may be showing the user's online dot. Fine at this scale; scoping
// per-chat would need a membership read-model this service deliberately avoids.

internal sealed class UserCameOnlineConsumer(IHubContext<RealtimeHub> hub) : IConsumer<UserCameOnlineIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserCameOnlineIntegrationEvent> context)
        => hub.Clients.All.SendAsync(
            RealtimeEventNames.PresenceChanged,
            new PresencePush(context.Message.UserId, IsOnline: true),
            context.CancellationToken);
}

internal sealed class UserWentOfflineConsumer(IHubContext<RealtimeHub> hub) : IConsumer<UserWentOfflineIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserWentOfflineIntegrationEvent> context)
        => hub.Clients.All.SendAsync(
            RealtimeEventNames.PresenceChanged,
            new PresencePush(context.Message.UserId, IsOnline: false),
            context.CancellationToken);
}
