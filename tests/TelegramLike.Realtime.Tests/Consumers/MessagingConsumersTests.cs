using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using MassTransit;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Contracts.Realtime;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Tests.Consumers;

public class MessagingConsumersTests
{
    [Fact]
    public async Task MessageSentConsumer_SendsToChatGroup_AndChatActivityToRecipientsPlusAuthor()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var recipient1 = Guid.NewGuid();
        var recipient2 = Guid.NewGuid();

        var evt = new MessageSentIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, messageId, chatId, authorId, new[] { recipient1, recipient2 });

        var (hub, clients) = HubTestDoubles.Create();
        var chatProxy = Substitute.For<IClientProxy>();
        var userProxy = Substitute.For<IClientProxy>();
        clients.Group(RealtimeGroups.Chat(chatId)).Returns(chatProxy);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(userProxy);

        var consumer = new MessageSentConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await chatProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.MessageSent,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<MessageSentPush>(a,
                p => p.MessageId == messageId && p.ChatId == chatId && p.AuthorId == authorId)),
            Arg.Any<CancellationToken>());

        // Author is not among the recipients, so ChatActivity fans to all three, deduped.
        clients.Received(1).Groups(Arg.Is<IReadOnlyList<string>>(g =>
            g.Count == 3 &&
            g.Contains(RealtimeGroups.User(recipient1)) &&
            g.Contains(RealtimeGroups.User(recipient2)) &&
            g.Contains(RealtimeGroups.User(authorId))));

        await userProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.ChatActivity,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<MessageSentPush>(a, _ => true)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageSentConsumer_WhenAuthorIsAlsoARecipient_DedupesUserGroups()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var recipient = Guid.NewGuid();

        // Author appears in Recipients (e.g. broadcast to self via another device) —
        // the consumer must not push ChatActivity to the same user group twice.
        var evt = new MessageSentIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), chatId, authorId, new[] { recipient, authorId });

        var (hub, clients) = HubTestDoubles.Create();
        var userProxy = Substitute.For<IClientProxy>();
        clients.Group(Arg.Any<string>()).Returns(Substitute.For<IClientProxy>());
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(userProxy);

        var consumer = new MessageSentConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        clients.Received(1).Groups(Arg.Is<IReadOnlyList<string>>(g =>
            g.Count == 2 &&
            g.Contains(RealtimeGroups.User(recipient)) &&
            g.Contains(RealtimeGroups.User(authorId))));
    }

    [Fact]
    public async Task MessageSentConsumer_OnALaterPart_PushesToThatSlicesUsersOnly_NotTheChatGroupAgain()
    {
        // A send into a large chat arrives as several parts ([TL-124]). The chat-group push is
        // per message, so repeating it per part would make every open client refetch the same
        // message once per part; the per-user push is per recipient, so this part still fans
        // out to its own slice. The author rides on part 0 only, so they are told exactly once.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var recipient = Guid.NewGuid();

        var evt = new MessageSentIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), chatId, authorId,
            new[] { recipient }, PartIndex: 1, PartCount: 2);

        var (hub, clients) = HubTestDoubles.Create();
        var chatProxy = Substitute.For<IClientProxy>();
        var userProxy = Substitute.For<IClientProxy>();
        clients.Group(RealtimeGroups.Chat(chatId)).Returns(chatProxy);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(userProxy);

        var consumer = new MessageSentConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        clients.DidNotReceive().Group(RealtimeGroups.Chat(chatId));
        clients.Received(1).Groups(Arg.Is<IReadOnlyList<string>>(g =>
            g.Count == 1 && g.Contains(RealtimeGroups.User(recipient))));
        await userProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.ChatActivity,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<MessageSentPush>(a, _ => true)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageRetractedConsumer_SendsOnlyToChatGroup()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var retractedBy = Guid.NewGuid();
        var evt = new MessageRetractedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, messageId, chatId, retractedBy);

        var (hub, clients) = HubTestDoubles.Create();
        var chatProxy = Substitute.For<IClientProxy>();
        clients.Group(RealtimeGroups.Chat(chatId)).Returns(chatProxy);

        var consumer = new MessageRetractedConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await chatProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.MessageRetracted,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<MessageRetractedPush>(a,
                p => p.MessageId == messageId && p.ChatId == chatId && p.RetractedBy == retractedBy)),
            Arg.Any<CancellationToken>());
        clients.DidNotReceive().Groups(Arg.Any<IReadOnlyList<string>>());
        _ = clients.DidNotReceive().All;
    }

    [Fact]
    public async Task ReactionAddedConsumer_SendsOnlyToChatGroup()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new ReactionAddedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, messageId, chatId, userId, "👍");

        var (hub, clients) = HubTestDoubles.Create();
        var chatProxy = Substitute.For<IClientProxy>();
        clients.Group(RealtimeGroups.Chat(chatId)).Returns(chatProxy);

        var consumer = new ReactionAddedConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await chatProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.ReactionAdded,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<ReactionPush>(a,
                p => p.MessageId == messageId && p.ChatId == chatId && p.UserId == userId && p.Emoji == "👍")),
            Arg.Any<CancellationToken>());
        clients.DidNotReceive().Groups(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task ReactionRemovedConsumer_SendsOnlyToChatGroup()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new ReactionRemovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, messageId, chatId, userId, "😂");

        var (hub, clients) = HubTestDoubles.Create();
        var chatProxy = Substitute.For<IClientProxy>();
        clients.Group(RealtimeGroups.Chat(chatId)).Returns(chatProxy);

        var consumer = new ReactionRemovedConsumer(hub);
        await consumer.Consume(HubTestDoubles.ContextFor(evt));

        await chatProxy.Received(1).SendCoreAsync(
            RealtimeEventNames.ReactionRemoved,
            Arg.Is<object?[]>(a => HubTestDoubles.SinglePayload<ReactionPush>(a,
                p => p.MessageId == messageId && p.ChatId == chatId && p.UserId == userId && p.Emoji == "😂")),
            Arg.Any<CancellationToken>());
        clients.DidNotReceive().Groups(Arg.Any<IReadOnlyList<string>>());
    }
}
