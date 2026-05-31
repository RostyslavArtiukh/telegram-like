using MassTransit;
using TelegramLike.Contracts.Messaging;

namespace TelegramLike.Web.Services.ChatChanged;

internal sealed class ReactionRemovedConsumer(IChatChangedPubSub pubsub)
    : IConsumer<ReactionRemovedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ReactionRemovedIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.ChatId);
}
