using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Messaging.Consumers;

// Revokes the whole chat's membership so no one keeps typing into a deleted chat.
internal sealed class ChatDeletedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<ChatDeletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ChatDeletedIntegrationEvent> context) =>
        readModel.DeactivateChatAsync(
            context.Message.ChatId, context.Message.OccurredAt, context.CancellationToken);
}
