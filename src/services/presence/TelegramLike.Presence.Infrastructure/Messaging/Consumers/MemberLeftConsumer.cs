using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Messaging.Consumers;

internal sealed class MemberLeftConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberLeftIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberLeftIntegrationEvent> context) =>
        readModel.DeactivateAsync(
            context.Message.ChatId, context.Message.UserId, context.Message.OccurredAt, context.CancellationToken);
}
