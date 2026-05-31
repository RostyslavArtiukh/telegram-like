using MediatR;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.AddReaction;

public sealed record AddReactionCommand(
    Guid MessageId,
    Guid UserId,
    Emoji Emoji,
    // Premium status lives in Identity. The Web BFF reads it from the session
    // and passes it in, so Messaging never has to call Identity.
    bool ActorIsPremium) : IRequest;
