using MediatR;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Application.Queries.GetChatMessages;

public sealed class GetChatMessagesQueryHandler(IMessageQueryService messageQueryService)
    : IRequestHandler<GetChatMessagesQuery, MessagePageDto>
{
    public Task<MessagePageDto> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize is < 1 or > 200 ? 50 : request.PageSize;

        // Used to fetch the chat through IChatRepository and assert that
        // RequesterId is an active member. Membership lives in Chats now, so
        // the Web BFF performs that check before calling Messaging.
        return messageQueryService.GetChatMessagesAsync(
            request.ChatId,
            request.RequesterId,
            request.BeforeSentAt,
            pageSize,
            cancellationToken);
    }
}
