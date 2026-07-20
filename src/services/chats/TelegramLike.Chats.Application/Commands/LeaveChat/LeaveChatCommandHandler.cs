using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.LeaveChat;

public sealed class LeaveChatCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
    : IRequestHandler<LeaveChatCommand>
{
    public async Task Handle(LeaveChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new DomainException("Chat not found.");

        chat.Leave(request.UserId);
        await chatRepository.UpdateAsync(chat, cancellationToken);
        metrics.RecordMembershipChange("left");
    }
}
