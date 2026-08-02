using MassTransit;
using TelegramLike.Contracts.Messaging;

namespace TelegramLike.Web.Services.NewMessage;

// The pubsub signal is per message, not per recipient — every circuit watching the chat
// refetches when it fires. A send into a large chat arrives as several parts ([TL-124]), so
// only the first one signals; the rest carry nothing this host needs.
internal sealed class NewMessageConsumer(NewMessagePubSub pubsub) : IConsumer<MessageSentIntegrationEvent>
{
    public Task Consume(ConsumeContext<MessageSentIntegrationEvent> context) =>
        context.Message.PartIndex == 0
            ? pubsub.PublishAsync(context.Message.ChatId, context.Message.MessageId)
            : Task.CompletedTask;
}
