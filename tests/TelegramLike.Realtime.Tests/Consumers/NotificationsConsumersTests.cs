using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Contracts.Realtime;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Tests.Consumers;

public class NotificationsConsumersTests
{
    [Fact]
    public async Task UnreadCountChangedConsumer_SignalsEachUsersGroup_WithNoPayload()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var evt = new UnreadCountChangedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, new[] { user1, user2 });

        var (hub, clients) = HubTestDoubles.Create();
        var userProxy = Substitute.For<IClientProxy>();
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(userProxy);

        var consumer = new UnreadCountChangedConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        clients.Received(1).Groups(Arg.Is<IReadOnlyList<string>>(g =>
            g.Count == 2 &&
            g.Contains(RealtimeGroups.User(user1)) &&
            g.Contains(RealtimeGroups.User(user2))));

        // Signal-only: no payload, clients refetch the count over HTTP.
        await userProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.UnreadCountChanged,
            Arg.Is<object?[]>(a => a.Length == 0),
            Arg.Any<CancellationToken>());
    }
}
