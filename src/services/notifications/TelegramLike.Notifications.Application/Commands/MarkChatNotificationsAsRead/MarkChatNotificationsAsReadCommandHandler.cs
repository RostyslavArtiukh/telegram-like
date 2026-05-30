using MediatR;
using TelegramLike.Notifications.Domain.Repositories;

namespace TelegramLike.Notifications.Application.Commands.MarkChatNotificationsAsRead;

public sealed class MarkChatNotificationsAsReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkChatNotificationsAsReadCommand>
{
    public Task Handle(MarkChatNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.RecipientId == Guid.Empty)
            throw new ArgumentException("RecipientId cannot be empty.", nameof(request));
        if (request.ChatId == Guid.Empty)
            throw new ArgumentException("ChatId cannot be empty.", nameof(request));

        return repository.MarkAllForChatAsReadAsync(
            request.RecipientId, request.ChatId, DateTime.UtcNow, cancellationToken);
    }
}
