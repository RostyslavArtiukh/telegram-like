using MassTransit;
using MediatR;
using TelegramLike.Contracts.Chats;
using TelegramLike.Notifications.Application.Commands.FanoutChatNotification;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Messaging.Consumers;

internal sealed class MemberKickedConsumer(IMediator mediator) : IConsumer<MemberKickedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberKickedIntegrationEvent> context) =>
        mediator.Send(
            new FanoutChatNotificationCommand(
                ChatId: context.Message.ChatId,
                ActorId: context.Message.KickedBy,
                Type: NotificationType.MemberKicked,
                Recipients: context.Message.Recipients),
            context.CancellationToken);
}
