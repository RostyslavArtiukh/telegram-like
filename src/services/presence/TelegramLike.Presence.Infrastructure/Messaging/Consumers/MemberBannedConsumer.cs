using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Messaging.Consumers;

// Keeps a banned member from broadcasting typing indicators into a chat they were
// removed from — StartTyping checks this read-model, not Chats.
internal sealed class MemberBannedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberBannedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberBannedIntegrationEvent> context) =>
        readModel.DeactivateAsync(
            context.Message.ChatId, context.Message.UserId, context.Message.OccurredAt, context.CancellationToken);
}
