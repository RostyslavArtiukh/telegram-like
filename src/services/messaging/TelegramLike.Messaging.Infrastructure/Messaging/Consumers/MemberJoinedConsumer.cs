using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

internal sealed class MemberJoinedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberJoinedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberJoinedIntegrationEvent> context) =>
        readModel.UpsertActiveAsync(context.Message.ChatId, context.Message.UserId, context.CancellationToken);
}
