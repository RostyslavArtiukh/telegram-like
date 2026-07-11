using MediatR;
using TelegramLike.Chats.Application.Queries;

namespace TelegramLike.Chats.Application.Queries.GetMyChats;

public sealed class GetMyChatsQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetMyChatsQuery, IReadOnlyList<ChatSummaryDto>>
{
    public Task<IReadOnlyList<ChatSummaryDto>> Handle(GetMyChatsQuery request, CancellationToken cancellationToken)
        => chatQueryService.GetMyChatsAsync(request.UserId, cancellationToken);
}
