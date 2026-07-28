using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.DeleteChat;

public sealed class DeleteChatCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
    : IRequestHandler<DeleteChatCommand>
{
    public async Task Handle(DeleteChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new DomainException("Chat not found.");

        // Soft delete — the aggregate stamps DeletedAt and refuses everything afterwards.
        // DirectChat overrides Delete to reject it outright; Owner-only is enforced inside.
        chat.Delete(request.DeletedByUserId);
        await chatRepository.UpdateAsync(chat, cancellationToken);
        metrics.RecordChatDeleted();
    }
}
