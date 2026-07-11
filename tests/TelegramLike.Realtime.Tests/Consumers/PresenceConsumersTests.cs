using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TelegramLike.Contracts.Presence;
using TelegramLike.Contracts.Realtime;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Tests.Consumers;

public class PresenceConsumersTests
{
    [Fact]
    public async Task UserTypingConsumer_SendsOnlyToChatGroup()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new UserTypingIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, userId);

        var (hub, clients) = HubTestDoubles.Create();
        var chatProxy = Substitute.For<IClientProxy>();
        clients.Group(RealtimeGroups.Chat(chatId)).Returns(chatProxy);

        var consumer = new UserTypingConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await chatProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.UserTyping,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<UserTypingPush>(a,
                p => p.ChatId == chatId && p.UserId == userId)),
            Arg.Any<CancellationToken>());
        clients.DidNotReceive().Groups(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task UserCameOnlineConsumer_BroadcastsToAllClients()
    {
        var userId = Guid.NewGuid();
        var evt = new UserCameOnlineIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, userId);

        var (hub, clients) = HubTestDoubles.Create();
        var allProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(allProxy);

        var consumer = new UserCameOnlineConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await allProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.PresenceChanged,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<PresencePush>(a,
                p => p.UserId == userId && p.IsOnline)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserWentOfflineConsumer_BroadcastsToAllClients()
    {
        var userId = Guid.NewGuid();
        var evt = new UserWentOfflineIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, userId);

        var (hub, clients) = HubTestDoubles.Create();
        var allProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(allProxy);

        var consumer = new UserWentOfflineConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await allProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.PresenceChanged,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<PresencePush>(a,
                p => p.UserId == userId && !p.IsOnline)),
            Arg.Any<CancellationToken>());
    }
}
