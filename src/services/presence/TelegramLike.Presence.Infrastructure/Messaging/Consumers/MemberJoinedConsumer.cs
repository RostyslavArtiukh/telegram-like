using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Messaging.Consumers;

internal sealed class MemberJoinedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberJoinedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberJoinedIntegrationEvent> context) =>
        readModel.UpsertActiveAsync(
            context.Message.ChatId, context.Message.UserId, context.Message.OccurredAt, context.CancellationToken);
}
