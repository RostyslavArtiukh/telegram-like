using MediatR;
using TelegramLike.Chats.Application.Common.Interfaces;

namespace TelegramLike.Chats.Application.Queries.GetChatById;

public sealed class GetChatByIdQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetChatByIdQuery, ChatDetailsDto?>
{
    public Task<ChatDetailsDto?> Handle(GetChatByIdQuery request, CancellationToken cancellationToken)
        => chatQueryService.GetChatByIdAsync(request.ChatId, cancellationToken);
}
