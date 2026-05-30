using MediatR;

namespace TelegramLike.Application.Messaging.Commands.MarkMessageAsRead;

public sealed record MarkMessageAsReadCommand(Guid MessageId, Guid ReaderUserId) : IRequest;
