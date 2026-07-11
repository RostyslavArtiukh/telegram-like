using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Messaging.Consumers;

internal sealed class MemberKickedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberKickedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberKickedIntegrationEvent> context) =>
        readModel.DeactivateAsync(
            context.Message.ChatId, context.Message.UserId, context.Message.OccurredAt, context.CancellationToken);
}
