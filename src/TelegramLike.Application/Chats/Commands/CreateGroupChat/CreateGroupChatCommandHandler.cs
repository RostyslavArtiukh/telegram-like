using MediatR;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Identity.Repositories;

namespace TelegramLike.Application.Chats.Commands.CreateGroupChat;

public sealed class CreateGroupChatCommandHandler(
    IChatRepository chatRepository,
    IUserRepository userRepository)
    : IRequestHandler<CreateGroupChatCommand, Guid>
{
    public async Task<Guid> Handle(CreateGroupChatCommand request, CancellationToken cancellationToken)
    {
        var owner = await userRepository.GetByIdAsync(request.OwnerUserId, cancellationToken)
                    ?? throw new InvalidOperationException("Owner user not found.");

        var chat = GroupChat.Create(ChatName.Create(request.Name), owner.Id);
        await chatRepository.AddAsync(chat, cancellationToken);
        return chat.Id;
    }
}
