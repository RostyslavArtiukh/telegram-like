using MediatR;
using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Application.Chats.Queries.GetChatById;

public sealed class GetChatByIdQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetChatByIdQuery, ChatDetailsDto?>
{
    public Task<ChatDetailsDto?> Handle(GetChatByIdQuery request, CancellationToken cancellationToken)
        => chatQueryService.GetChatByIdAsync(request.ChatId, cancellationToken);
}
