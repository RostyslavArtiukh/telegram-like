using MediatR;
using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Application.Presence.Commands.StopTyping;

public sealed class StopTypingCommandHandler(ITypingIndicatorService typingService)
    : IRequestHandler<StopTypingCommand>
{
    public Task Handle(StopTypingCommand request, CancellationToken cancellationToken)
        => typingService.StopTypingAsync(request.ChatId, request.UserId, cancellationToken);
}
