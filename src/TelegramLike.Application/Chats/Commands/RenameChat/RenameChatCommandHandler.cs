using MediatR;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Chats.Commands.RenameChat;

public sealed class RenameChatCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<RenameChatCommand>
{
    public async Task Handle(RenameChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        chat.Rename(ChatName.Create(request.NewName), request.ActorUserId);
        await chatRepository.UpdateAsync(chat, cancellationToken);
    }
}
