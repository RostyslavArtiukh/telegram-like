using MediatR;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.TransferOwnership;

public sealed class TransferOwnershipCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<TransferOwnershipCommand>
{
    public async Task Handle(TransferOwnershipCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        switch (chat)
        {
            case GroupChat group:
                group.TransferOwnership(request.NewOwnerUserId, request.CurrentOwnerUserId);
                break;
            case BroadcastChannel broadcast:
                broadcast.TransferOwnership(request.NewOwnerUserId, request.CurrentOwnerUserId);
                break;
            default:
                throw new InvalidOperationException("This chat type does not support ownership transfer.");
        }

        await chatRepository.UpdateAsync(chat, cancellationToken);
    }
}
