using MassTransit;
using TelegramLike.Contracts.Presence;

namespace TelegramLike.Web.Services.Typing;

internal sealed class UserTypingConsumer(ITypingPubSub pubsub) : IConsumer<UserTypingIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserTypingIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.ChatId, context.Message.UserId);
}
