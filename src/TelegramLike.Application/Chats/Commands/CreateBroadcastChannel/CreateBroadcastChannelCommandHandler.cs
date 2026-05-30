using MediatR;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Identity.Repositories;

namespace TelegramLike.Application.Chats.Commands.CreateBroadcastChannel;

public sealed class CreateBroadcastChannelCommandHandler(
    IChatRepository chatRepository,
    IUserRepository userRepository)
    : IRequestHandler<CreateBroadcastChannelCommand, Guid>
{
    public async Task<Guid> Handle(CreateBroadcastChannelCommand request, CancellationToken cancellationToken)
    {
        var owner = await userRepository.GetByIdAsync(request.OwnerUserId, cancellationToken)
                    ?? throw new InvalidOperationException("Owner user not found.");

        var chat = BroadcastChannel.Create(ChatName.Create(request.Name), owner.Id);
        await chatRepository.AddAsync(chat, cancellationToken);
        return chat.Id;
    }
}
