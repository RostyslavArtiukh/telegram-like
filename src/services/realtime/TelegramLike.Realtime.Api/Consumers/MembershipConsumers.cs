using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Realtime.Api.Membership;

namespace TelegramLike.Realtime.Api.Consumers;

// Feed the in-memory membership tracker so JoinChat can reject non-members of a known
// chat. Per-instance queues mean every replica maintains its own tracker for its own
// connections. These update no hub group — they only maintain the authorization view.

internal sealed class MemberJoinedMembershipConsumer(ChatMembershipTracker tracker)
    : IConsumer<MemberJoinedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberJoinedIntegrationEvent> context)
    {
        tracker.Join(context.Message.ChatId, context.Message.UserId);
        return Task.CompletedTask;
    }
}

internal sealed class MemberLeftMembershipConsumer(ChatMembershipTracker tracker)
    : IConsumer<MemberLeftIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberLeftIntegrationEvent> context)
    {
        tracker.Leave(context.Message.ChatId, context.Message.UserId);
        return Task.CompletedTask;
    }
}

internal sealed class MemberKickedMembershipConsumer(ChatMembershipTracker tracker)
    : IConsumer<MemberKickedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberKickedIntegrationEvent> context)
    {
        tracker.Leave(context.Message.ChatId, context.Message.UserId);
        return Task.CompletedTask;
    }
}
