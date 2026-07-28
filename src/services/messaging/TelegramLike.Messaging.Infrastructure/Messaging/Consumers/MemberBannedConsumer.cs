using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

// Without this a ban would only stop the user rejoining in Chats: Messaging decides
// membership from its own read-model, so the banned user could keep sending messages,
// reacting and retracting.
internal sealed class MemberBannedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberBannedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberBannedIntegrationEvent> context) =>
        readModel.DeactivateAsync(
            context.Message.ChatId, context.Message.UserId, context.Message.OccurredAt, context.CancellationToken);
}
