using MassTransit;
using TelegramLike.Contracts.Messaging;

namespace TelegramLike.Web.Services.ChatChanged;

internal sealed class MessageRetractedConsumer(ChatChangedPubSub pubsub)
    : IConsumer<MessageRetractedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MessageRetractedIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.ChatId);
}
