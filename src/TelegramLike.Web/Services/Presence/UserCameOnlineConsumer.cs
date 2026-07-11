using MassTransit;
using TelegramLike.Contracts.Presence;

namespace TelegramLike.Web.Services.Presence;

internal sealed class UserCameOnlineConsumer(PresencePubSub pubsub)
    : IConsumer<UserCameOnlineIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserCameOnlineIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.UserId, isOnline: true);
}
