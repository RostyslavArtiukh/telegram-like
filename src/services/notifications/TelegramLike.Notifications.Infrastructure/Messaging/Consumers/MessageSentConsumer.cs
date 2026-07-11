using MassTransit;
using MediatR;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Notifications.Application.Commands.FanoutChatNotification;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Infrastructure.Messaging.Consumers;

internal sealed class MessageSentConsumer(IMediator mediator) : IConsumer<MessageSentIntegrationEvent>
{
    public Task Consume(ConsumeContext<MessageSentIntegrationEvent> context) =>
        mediator.Send(
            new FanoutChatNotificationCommand(
                ChatId: context.Message.ChatId,
                TriggeredByUserId: context.Message.AuthorId,
                Type: NotificationType.NewMessage,
                Recipients: context.Message.Recipients,
                SourceEventId: context.Message.EventId,
                MessageId: context.Message.MessageId),
            context.CancellationToken);
}
