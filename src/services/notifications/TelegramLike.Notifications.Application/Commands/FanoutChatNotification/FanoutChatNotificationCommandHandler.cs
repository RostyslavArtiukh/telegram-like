using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Application.Commands.FanoutChatNotification;

public sealed class FanoutChatNotificationCommandHandler(
    INotificationRepository notificationRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<FanoutChatNotificationCommandHandler> logger)
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
            .Select(r => Notification.Create(r, request.Type, payload, request.SourceEventId))
            .ToList();

        var inserted = await notificationRepository.AddManyIgnoringDuplicatesAsync(notifications, cancellationToken);

        if (inserted < notifications.Count)
            logger.LogInformation(
                "Fanout {EventId}: {Inserted}/{Total} new notifications (rest were redeliveries)",
                request.SourceEventId, inserted, notifications.Count);

        // Publish independently of the insert count. Gating on inserted>0 loses the
        // signal permanently in the fail-after-insert case: if a prior delivery wrote
        // the rows but the publish threw, the redelivery dedup-skips (inserted==0) and
        // would never re-signal. inserted==0 does NOT prove the badge already refreshed,
        // so re-publishing (an at-most-cheap refetch) is the safe choice. There are
        // always recipients here (we returned early on none). No transactional outbox
        // in Notifications, so this is the idempotent-publish mitigation.
        await publishEndpoint.Publish(new UnreadCountChangedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserIds: recipients), cancellationToken);
    }
}
