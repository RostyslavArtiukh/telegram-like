using MediatR;

namespace TelegramLike.Notifications.Application.Commands.MarkChatNotificationsAsRead;

public sealed record MarkChatNotificationsAsReadCommand(Guid RecipientId, Guid ChatId) : IRequest;
