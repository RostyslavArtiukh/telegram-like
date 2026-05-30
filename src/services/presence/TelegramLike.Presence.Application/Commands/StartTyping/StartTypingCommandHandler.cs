using MediatR;
using TelegramLike.Presence.Application.Abstractions;

namespace TelegramLike.Presence.Application.Commands.StartTyping;

// Chat membership check was dropped during the microservice extraction (Day 15) —
// Presence-service has no cross-context access to IChatRepository. JWT-authenticated
// caller is currently trusted. To restore strict validation, subscribe to
// MemberJoined/Left/Kicked integration events and maintain a local read model.
public sealed class StartTypingCommandHandler(ITypingIndicatorService typingService)
    : IRequestHandler<StartTypingCommand>
{
    public Task Handle(StartTypingCommand request, CancellationToken cancellationToken)
        => typingService.StartTypingAsync(request.ChatId, request.UserId, cancellationToken);
}
