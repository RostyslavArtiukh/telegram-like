using MediatR;

namespace TelegramLike.Application.Messaging.Commands.HideMessage;

public sealed record HideMessageCommand(Guid MessageId, Guid UserId) : IRequest;
