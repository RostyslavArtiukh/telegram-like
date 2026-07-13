using MassTransit;
using MediatR;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Domain.Repositories;

namespace TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;

public sealed class MarkAllNotificationsAsReadCommandHandler(
    INotificationRepository repository,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<MarkAllNotificationsAsReadCommand>
{
    public async Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.RecipientId == Guid.Empty)
            throw new DomainException("RecipientId cannot be empty.");

        var changed = await repository.MarkAllAsReadAsync(request.RecipientId, DateTime.UtcNow, cancellationToken);

        // Nothing was unread → the count didn't change → no need to make the badge refetch.
        if (changed == 0) return;

        await publishEndpoint.Publish(new UnreadCountChangedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserIds: new[] { request.RecipientId }), cancellationToken);
    }
}
