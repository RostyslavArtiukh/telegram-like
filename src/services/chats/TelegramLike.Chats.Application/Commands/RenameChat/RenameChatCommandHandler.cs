using MediatR;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.RenameChat;

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
