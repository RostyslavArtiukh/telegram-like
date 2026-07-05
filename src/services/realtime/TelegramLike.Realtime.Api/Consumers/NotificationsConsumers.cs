using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Contracts.Realtime;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Api.Consumers;

internal sealed class UnreadCountChangedConsumer(IHubContext<RealtimeHub> hub) : IConsumer<UnreadCountChangedIntegrationEvent>
{
    // Signal-only, like the integration event itself: the client refetches the
    // count over HTTP to avoid stale-read races between concurrent operations.
    public Task Consume(ConsumeContext<UnreadCountChangedIntegrationEvent> context)
    {
        var userGroups = context.Message.UserIds.Select(RealtimeGroups.User).ToList();
        return hub.Clients.Groups(userGroups)
            .SendAsync(RealtimeEventNames.UnreadCountChanged, context.CancellationToken);
    }
}
