using MassTransit;
using MediatR;
using TelegramLike.Contracts.Chats;
using TelegramLike.Notifications.Application.Commands.FanoutChatNotification;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Messaging.Consumers;

internal sealed class MemberJoinedConsumer(IMediator mediator) : IConsumer<MemberJoinedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberJoinedIntegrationEvent> context) =>
        mediator.Send(
            new FanoutChatNotificationCommand(
                ChatId: context.Message.ChatId,
                TriggeredByUserId: context.Message.UserId,
                Type: NotificationType.MemberJoined,
                Recipients: context.Message.Recipients,
                SourceEventId: context.Message.EventId),
            context.CancellationToken);
}
