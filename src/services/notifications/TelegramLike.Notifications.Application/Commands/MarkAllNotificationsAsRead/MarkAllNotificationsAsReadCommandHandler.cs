using MediatR;
using TelegramLike.Notifications.Domain.Repositories;

namespace TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;

public sealed class MarkAllNotificationsAsReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkAllNotificationsAsReadCommand>
{
    public Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.RecipientId == Guid.Empty)
            throw new ArgumentException("RecipientId cannot be empty.", nameof(request));

        return repository.MarkAllAsReadAsync(request.RecipientId, DateTime.UtcNow, cancellationToken);
    }
}
