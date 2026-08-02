using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Contracts.Realtime;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Api.Consumers;

// Each consumer relays one integration event into hub groups. "MessageSent" goes to
// the chat group (open-chat views); "ChatActivity" goes to per-user groups
// (recipients + author's other devices) so the chat list updates without every
// client joining every chat — and no client gets the same semantic event twice.

internal sealed class MessageSentConsumer(IHubContext<RealtimeHub> hub) : IConsumer<MessageSentIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MessageSentIntegrationEvent> context)
    {
        var e = context.Message;
        var push = new MessageSentPush(e.MessageId, e.ChatId, e.AuthorId);

        // The chat-group push is per MESSAGE, so it belongs to the first part only. A large
        // chat's send arrives as several parts ([TL-124]); pushing on each would tell every
        // open client to refetch the same message once per part.
        if (e.PartIndex == 0)
        {
            await hub.Clients.Group(RealtimeGroups.Chat(e.ChatId))
                .SendAsync(RealtimeEventNames.MessageSent, push, context.CancellationToken);
        }

        // The per-user push is per RECIPIENT, so each part pushes to its own slice. The author
        // rides along with the first part so their other devices are told exactly once.
        var userGroups = e.Recipients
            .Concat(e.PartIndex == 0 ? [e.AuthorId] : Array.Empty<Guid>())
            .Distinct()
            .Select(RealtimeGroups.User)
            .ToList();
        await hub.Clients.Groups(userGroups)
            .SendAsync(RealtimeEventNames.ChatActivity, push, context.CancellationToken);
    }
}

internal sealed class MessageRetractedConsumer(IHubContext<RealtimeHub> hub) : IConsumer<MessageRetractedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MessageRetractedIntegrationEvent> context)
    {
        var e = context.Message;
        return hub.Clients.Group(RealtimeGroups.Chat(e.ChatId)).SendAsync(
            RealtimeEventNames.MessageRetracted,
            new MessageRetractedPush(e.MessageId, e.ChatId, e.RetractedBy),
            context.CancellationToken);
    }
}

internal sealed class ReactionAddedConsumer(IHubContext<RealtimeHub> hub) : IConsumer<ReactionAddedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ReactionAddedIntegrationEvent> context)
    {
        var e = context.Message;
        return hub.Clients.Group(RealtimeGroups.Chat(e.ChatId)).SendAsync(
            RealtimeEventNames.ReactionAdded,
            new ReactionPush(e.MessageId, e.ChatId, e.UserId, e.Emoji),
            context.CancellationToken);
    }
}

internal sealed class ReactionRemovedConsumer(IHubContext<RealtimeHub> hub) : IConsumer<ReactionRemovedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ReactionRemovedIntegrationEvent> context)
    {
        var e = context.Message;
        return hub.Clients.Group(RealtimeGroups.Chat(e.ChatId)).SendAsync(
            RealtimeEventNames.ReactionRemoved,
            new ReactionPush(e.MessageId, e.ChatId, e.UserId, e.Emoji),
            context.CancellationToken);
    }
}
