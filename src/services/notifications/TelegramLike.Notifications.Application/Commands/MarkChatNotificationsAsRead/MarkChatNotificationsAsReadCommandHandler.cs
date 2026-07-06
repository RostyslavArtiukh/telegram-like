using MassTransit;
using MediatR;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Domain.Repositories;

namespace TelegramLike.Notifications.Application.Commands.MarkChatNotificationsAsRead;

public sealed class MarkChatNotificationsAsReadCommandHandler(
    INotificationRepository repository,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<MarkChatNotificationsAsReadCommand>
{
    public async Task Handle(MarkChatNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.RecipientId == Guid.Empty)
            throw new ArgumentException("RecipientId cannot be empty.", nameof(request));
        if (request.ChatId == Guid.Empty)
            throw new ArgumentException("ChatId cannot be empty.", nameof(request));

        var changed = await repository.MarkAllForChatAsReadAsync(
            request.RecipientId, request.ChatId, DateTime.UtcNow, cancellationToken);

        // Nothing was unread for this chat → the count didn't change → skip the refetch signal.
        if (changed == 0) return;

        await publishEndpoint.Publish(new UnreadCountChangedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserIds: new[] { request.RecipientId }), cancellationToken);
    }
}
