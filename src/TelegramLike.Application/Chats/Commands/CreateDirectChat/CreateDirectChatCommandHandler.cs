using MediatR;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Identity.Repositories;

namespace TelegramLike.Application.Chats.Commands.CreateDirectChat;

public sealed class CreateDirectChatCommandHandler(
    IChatRepository chatRepository,
    IUserRepository userRepository)
    : IRequestHandler<CreateDirectChatCommand, Guid>
{
    public async Task<Guid> Handle(CreateDirectChatCommand request, CancellationToken cancellationToken)
    {
        var initiator = await userRepository.GetByIdAsync(request.InitiatorUserId, cancellationToken)
                        ?? throw new InvalidOperationException("Initiator user not found.");
        var peer = await userRepository.GetByIdAsync(request.PeerUserId, cancellationToken)
                   ?? throw new InvalidOperationException("Peer user not found.");

        if (initiator.HasBlocked(peer.Id) || peer.HasBlocked(initiator.Id))
            throw new InvalidOperationException("Cannot start a direct chat: one of the users is blocked.");

        var existing = await chatRepository.FindDirectBetweenAsync(initiator.Id, peer.Id, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var chat = DirectChat.Create(initiator.Id, peer.Id);
        await chatRepository.AddAsync(chat, cancellationToken);
        return chat.Id;
    }
}
