using MediatR;
using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Application.Chats.Queries.GetMyChats;

public sealed class GetMyChatsQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetMyChatsQuery, IReadOnlyList<ChatSummaryDto>>
{
    public Task<IReadOnlyList<ChatSummaryDto>> Handle(GetMyChatsQuery request, CancellationToken cancellationToken)
        => chatQueryService.GetMyChatsAsync(request.UserId, cancellationToken);
}
