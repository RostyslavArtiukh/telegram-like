using MassTransit;
using TelegramLike.Contracts.Presence;

namespace TelegramLike.Web.Services.Presence;

internal sealed class UserCameOnlineConsumer(IPresencePubSub pubsub)
    : IConsumer<UserCameOnlineIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserCameOnlineIntegrationEvent> context) =>
        pubsub.PublishAsync(context.Message.UserId, isOnline: true);
}
