using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

// Materializes a chat's type into the local read-model so SendMessage can derive isBroadcast
// server-side. Set-once and idempotent — chat type never changes.
internal sealed class ChatCreatedConsumer(IChatTypeReadModel readModel)
    : IConsumer<ChatCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ChatCreatedIntegrationEvent> context) =>
        readModel.UpsertAsync(context.Message.ChatId, context.Message.Type, context.CancellationToken);
}
