using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Api.Consumers;

// Keep this replica's membership answers fresh. These update no hub group — they only
// maintain the authorization view.
//
// They REFRESH, they do not materialize ([TL-127]): an event about a pair nobody here has
// asked about is dropped. Caching it would rebuild the thing this service used to be — a full
// in-memory copy of every membership in the system, on every replica. What isn't cached isn't
// lost either: the first JoinChat for it asks Chats directly.

internal sealed class MemberJoinedMembershipConsumer(ChatMembershipCheck membership)
    : IConsumer<MemberJoinedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberJoinedIntegrationEvent> context)
    {
        membership.Refresh(context.Message.ChatId, context.Message.UserId, isMember: true);
        return Task.CompletedTask;
    }
}

internal sealed class MemberLeftMembershipConsumer(ChatMembershipCheck membership)
    : IConsumer<MemberLeftIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberLeftIntegrationEvent> context)
    {
        membership.Refresh(context.Message.ChatId, context.Message.UserId, isMember: false);
        return Task.CompletedTask;
    }
}

internal sealed class MemberKickedMembershipConsumer(ChatMembershipCheck membership)
    : IConsumer<MemberKickedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberKickedIntegrationEvent> context)
    {
        membership.Refresh(context.Message.ChatId, context.Message.UserId, isMember: false);
        return Task.CompletedTask;
    }
}

internal sealed class MemberBannedMembershipConsumer(ChatMembershipCheck membership)
    : IConsumer<MemberBannedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberBannedIntegrationEvent> context)
    {
        membership.Refresh(context.Message.ChatId, context.Message.UserId, isMember: false);
        return Task.CompletedTask;
    }
}

// Chats' own member lookup ignores DeletedAt, so this event is the only thing that stops a
// deleted chat still answering "yes, a member". See ChatMembershipCheck.Revoke.
internal sealed class ChatDeletedMembershipConsumer(ChatMembershipCheck membership)
    : IConsumer<ChatDeletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ChatDeletedIntegrationEvent> context)
    {
        membership.Revoke(context.Message.ChatId);
        return Task.CompletedTask;
    }
}
