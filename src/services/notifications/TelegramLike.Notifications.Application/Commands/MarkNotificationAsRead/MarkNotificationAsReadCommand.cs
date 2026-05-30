using MediatR;

namespace TelegramLike.Notifications.Application.Commands.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(Guid NotificationId, Guid RecipientId) : IRequest;
