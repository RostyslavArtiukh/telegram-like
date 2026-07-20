using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.CreateDirectChat;

public sealed class CreateDirectChatCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
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

        var chatId = request.ChatId == Guid.Empty ? Guid.NewGuid() : request.ChatId;
        var chat = DirectChat.Create(chatId, request.InitiatorUserId, request.PeerUserId);
        await chatRepository.AddAsync(chat, cancellationToken);

        // Only reached when nothing existed — the early return above means a repeated
        // "open a DM with X" doesn't inflate the created count.
        metrics.RecordChatCreated("direct");

        return chat.Id;
    }
}
