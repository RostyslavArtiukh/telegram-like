using MediatR;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.CreateDirectChat;

public sealed class CreateDirectChatCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<CreateDirectChatCommand, Guid>
{
    public async Task<Guid> Handle(CreateDirectChatCommand request, CancellationToken cancellationToken)
    {
        // The monolith version called IUserRepository to (a) ensure both users
        // exist and (b) reject the chat if one had blocked the other. After
        // extraction the blocklist lives in Identity — we trust the Web BFF to
        // refuse the call when the initiator is blocked. A proper restoration
        // would be a local UserBlockReadModel populated from Identity events.
        var existing = await chatRepository.FindDirectBetweenAsync(
            request.InitiatorUserId,
            request.PeerUserId,
            cancellationToken);
        if (existing is not null)
            return existing.Id;

        var chat = DirectChat.Create(request.InitiatorUserId, request.PeerUserId);
        await chatRepository.AddAsync(chat, cancellationToken);
        return chat.Id;
    }
}
