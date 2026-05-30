using MediatR;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Repositories;

namespace TelegramLike.Application.Chats.Commands.JoinChat;

public sealed class JoinChatCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<JoinChatCommand>
{
    public async Task Handle(JoinChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        switch (chat)
        {
            case GroupChat group:
                group.Join(request.UserId);
                break;
            case BroadcastChannel broadcast:
                broadcast.Join(request.UserId);
                break;
            default:
                throw new InvalidOperationException("This chat type does not support Join.");
        }

        await chatRepository.UpdateAsync(chat, cancellationToken);
    }
}
