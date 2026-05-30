using MediatR;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Application.Commands.FanoutChatNotification;

public sealed class FanoutChatNotificationCommandHandler(
    INotificationRepository notificationRepository)
    : IRequestHandler<FanoutChatNotificationCommand>
{
    public async Task Handle(FanoutChatNotificationCommand request, CancellationToken cancellationToken)
    {
        var recipients = request.Recipients
            .Where(r => r != request.ActorId)
            .Distinct()
            .ToList();

        if (recipients.Count == 0) return;

        var payload = request.Type switch
        {
            NotificationType.NewMessage or NotificationType.MentionInGroup
                when request.MessageId.HasValue
                => NotificationPayload.ForNewMessage(request.ChatId, request.MessageId.Value, request.ActorId),

            NotificationType.MemberJoined => NotificationPayload.ForMemberJoined(request.ChatId, request.ActorId),
            NotificationType.MemberKicked => NotificationPayload.ForMemberKicked(request.ChatId, request.ActorId),

            _ => throw new InvalidOperationException(
                $"Notification type {request.Type} requires a MessageId.")
        };

        var notifications = recipients
            .Select(r => Notification.Create(r, request.Type, payload))
            .ToList();

        await notificationRepository.AddManyAsync(notifications, cancellationToken);
    }
}
