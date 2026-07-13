using MediatR;

namespace TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

public sealed record MarkMessageAsReadCommand(
    Guid MessageId,
    Guid ReaderUserId) : IRequest;
