using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

internal sealed class MemberKickedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberKickedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberKickedIntegrationEvent> context) =>
        readModel.RemoveAsync(context.Message.ChatId, context.Message.UserId, context.CancellationToken);
}
