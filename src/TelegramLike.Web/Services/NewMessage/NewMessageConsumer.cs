using MassTransit;
using TelegramLike.Contracts.Messaging;

namespace TelegramLike.Web.Services.NewMessage;

internal sealed class NewMessageConsumer(NewMessagePubSub pubsub) : IConsumer<MessageSentIntegrationEvent>
{
    public Task Consume(ConsumeContext<MessageSentIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.ChatId, context.Message.MessageId);
}
