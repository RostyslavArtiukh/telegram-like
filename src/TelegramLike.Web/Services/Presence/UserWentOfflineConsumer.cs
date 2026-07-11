using MassTransit;
using TelegramLike.Contracts.Presence;

namespace TelegramLike.Web.Services.Presence;

internal sealed class UserWentOfflineConsumer(PresencePubSub pubsub)
    : IConsumer<UserWentOfflineIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserWentOfflineIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.UserId, isOnline: false);
}
