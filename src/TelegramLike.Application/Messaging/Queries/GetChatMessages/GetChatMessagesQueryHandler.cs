using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Chats.Repositories;

namespace TelegramLike.Application.Messaging.Queries.GetChatMessages;

public sealed class GetChatMessagesQueryHandler(
    IChatRepository chatRepository,
    IMessageQueryService messageQueryService)
    : IRequestHandler<GetChatMessagesQuery, MessagePageDto>
{
    public async Task<MessagePageDto> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize is < 1 or > 200 ? 50 : request.PageSize;

        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        if (chat.FindActiveMember(request.RequesterId) is null)
            throw new InvalidOperationException("Only active chat members can read messages.");

        return await messageQueryService.GetChatMessagesAsync(
            request.ChatId,
            request.RequesterId,
            request.BeforeSentAt,
            pageSize,
            cancellationToken);
    }
}
