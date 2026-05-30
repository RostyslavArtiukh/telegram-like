using MassTransit;
using MediatR;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Domain.Repositories;

namespace TelegramLike.Notifications.Application.Commands.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadCommandHandler(
    INotificationRepository repository,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<MarkNotificationAsReadCommand>
{
    public async Task Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await repository.GetByIdAsync(request.NotificationId, cancellationToken)
                           ?? throw new InvalidOperationException("Notification not found.");

        if (notification.RecipientId != request.RecipientId)
            throw new InvalidOperationException("Cannot mark another user's notification as read.");

        notification.MarkAsRead();
        await repository.UpdateAsync(notification, cancellationToken);

        await publishEndpoint.Publish(new UnreadCountChangedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserIds: new[] { request.RecipientId }), cancellationToken);
    }
}
