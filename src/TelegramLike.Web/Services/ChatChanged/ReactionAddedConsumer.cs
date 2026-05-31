using MassTransit;
using TelegramLike.Contracts.Messaging;

namespace TelegramLike.Web.Services.ChatChanged;

internal sealed class ReactionAddedConsumer(IChatChangedPubSub pubsub)
    : IConsumer<ReactionAddedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ReactionAddedIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.ChatId);
}
