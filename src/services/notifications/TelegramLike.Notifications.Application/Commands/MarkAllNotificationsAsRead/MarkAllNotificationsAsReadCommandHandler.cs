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
            throw new ArgumentException("RecipientId cannot be empty.", nameof(request));

        await repository.MarkAllAsReadAsync(request.RecipientId, DateTime.UtcNow, cancellationToken);

        await publishEndpoint.Publish(new UnreadCountChangedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserIds: new[] { request.RecipientId }), cancellationToken);
    }
}
