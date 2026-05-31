using MediatR;

namespace TelegramLike.Messaging.Application.Commands.RetractMessage;

public sealed record RetractMessageCommand(
    Guid MessageId,
    Guid ActorUserId,
    // Web BFF tells us whether this actor is an owner/admin of the chat. With
    // Chats in its own service, Messaging can't look that up itself.
    bool ActorIsModerator) : IRequest;
