using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.CreateGroupChat;

public sealed class CreateGroupChatCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
    : IRequestHandler<CreateGroupChatCommand, Guid>
{
    public async Task<Guid> Handle(CreateGroupChatCommand request, CancellationToken cancellationToken)
    {
        // The monolith version fetched IUserRepository to verify the owner exists.
        // Identity lives in a different service now, so we trust the JWT-authenticated
        // caller (the Web BFF) to pass a real user id.
        var chatId = request.ChatId == Guid.Empty ? Guid.NewGuid() : request.ChatId;
        var chat = GroupChat.Create(chatId, ChatName.Create(request.Name), request.OwnerUserId);
        await chatRepository.AddAsync(chat, cancellationToken);
        metrics.RecordChatCreated("group");
        return chat.Id;
    }
}
