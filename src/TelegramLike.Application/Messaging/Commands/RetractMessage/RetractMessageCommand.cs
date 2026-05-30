using MediatR;

namespace TelegramLike.Application.Messaging.Commands.RetractMessage;

public sealed record RetractMessageCommand(Guid MessageId, Guid ActorUserId) : IRequest;
