using MassTransit;
using TelegramLike.Contracts.Notifications;

namespace TelegramLike.Web.Services.UnreadCount;

internal sealed class UnreadCountChangedConsumer(IUnreadCountPubSub pubsub) : IConsumer<UnreadCountChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UnreadCountChangedIntegrationEvent> context)
    {
        foreach (var userId in context.Message.UserIds)
            await pubsub.PublishAsync(userId);
    }
}
