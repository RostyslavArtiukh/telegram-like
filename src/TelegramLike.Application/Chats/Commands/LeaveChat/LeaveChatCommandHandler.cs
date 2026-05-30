using MediatR;
using TelegramLike.Domain.Chats.Repositories;

namespace TelegramLike.Application.Chats.Commands.LeaveChat;

public sealed class LeaveChatCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<LeaveChatCommand>
{
    public async Task Handle(LeaveChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        chat.Leave(request.UserId);
        await chatRepository.UpdateAsync(chat, cancellationToken);
    }
}
