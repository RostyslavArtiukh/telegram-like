using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.JoinChat;

public sealed class JoinChatCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
    : IRequestHandler<JoinChatCommand>
{
    public async Task Handle(JoinChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new DomainException("Chat not found.");

        switch (chat)
        {
            case GroupChat group:
                group.Join(request.UserId);
                break;
            case BroadcastChannel broadcast:
                broadcast.Join(request.UserId);
                break;
            default:
                throw new DomainException("This chat type does not support Join.");
        }

        await chatRepository.UpdateAsync(chat, cancellationToken);
        metrics.RecordMembershipChange("joined");
    }
}
