using MediatR;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.AddReaction;

public sealed record AddReactionCommand(
    Guid MessageId,
    Guid UserId,
    Emoji Emoji,
    // Premium status lives in Identity. It is read server-side from the signed `premium`
    // JWT claim by the controller ([TL-102]) — no longer a spoofable client-supplied flag.
    bool UserIsPremium) : IRequest;
