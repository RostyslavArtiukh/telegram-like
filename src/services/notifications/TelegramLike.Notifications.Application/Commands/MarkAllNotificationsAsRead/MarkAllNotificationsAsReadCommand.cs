using MediatR;

namespace TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;

public sealed record MarkAllNotificationsAsReadCommand(Guid RecipientId) : IRequest;
