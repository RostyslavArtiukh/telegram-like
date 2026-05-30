using MediatR;
using TelegramLike.Presence.Application.Abstractions;

namespace TelegramLike.Presence.Application.Commands.StopTyping;

public sealed class StopTypingCommandHandler(ITypingIndicatorService typingService)
    : IRequestHandler<StopTypingCommand>
{
    public Task Handle(StopTypingCommand request, CancellationToken cancellationToken)
        => typingService.StopTypingAsync(request.ChatId, request.UserId, cancellationToken);
}
