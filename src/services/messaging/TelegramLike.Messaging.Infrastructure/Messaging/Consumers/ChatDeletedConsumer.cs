using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

// Chats stamps DeletedAt and refuses further chat operations, but Messaging decides
// membership from its own read-model — without this, members would go on sending
// messages and reacting in a chat that no longer exists.
internal sealed class ChatDeletedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<ChatDeletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ChatDeletedIntegrationEvent> context) =>
        readModel.DeactivateChatAsync(
            context.Message.ChatId, context.Message.OccurredAt, context.CancellationToken);
}
