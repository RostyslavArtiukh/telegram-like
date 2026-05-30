using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Chats.Repositories;

namespace TelegramLike.Application.Presence.Commands.StartTyping;

public sealed class StartTypingCommandHandler(
    IChatRepository chatRepository,
    ITypingIndicatorService typingService)
    : IRequestHandler<StartTypingCommand>
{
    public async Task Handle(StartTypingCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        if (chat.FindActiveMember(request.UserId) is null)
            throw new InvalidOperationException("Only active chat members can broadcast typing indicators.");

        await typingService.StartTypingAsync(request.ChatId, request.UserId, cancellationToken);
    }
}
