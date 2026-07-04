using MediatR;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;

public sealed class CreateBroadcastChannelCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<CreateBroadcastChannelCommand, Guid>
{
    public async Task<Guid> Handle(CreateBroadcastChannelCommand request, CancellationToken cancellationToken)
    {
        // Owner existence is the Identity service's responsibility now — we trust
        // the JWT-authenticated caller.
        var chatId = request.ChatId == Guid.Empty ? Guid.NewGuid() : request.ChatId;
        var chat = BroadcastChannel.Create(chatId, ChatName.Create(request.Name), request.OwnerUserId);
        await chatRepository.AddAsync(chat, cancellationToken);
        return chat.Id;
    }
}
