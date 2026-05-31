using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

internal sealed class MemberLeftConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberLeftIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberLeftIntegrationEvent> context) =>
        readModel.RemoveAsync(context.Message.ChatId, context.Message.UserId, context.CancellationToken);
}
