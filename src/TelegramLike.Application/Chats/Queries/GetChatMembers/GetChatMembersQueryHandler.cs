using MediatR;
using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Application.Chats.Queries.GetChatMembers;

public sealed class GetChatMembersQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetChatMembersQuery, IReadOnlyList<ChatMemberDto>>
{
    public Task<IReadOnlyList<ChatMemberDto>> Handle(GetChatMembersQuery request, CancellationToken cancellationToken)
        => chatQueryService.GetChatMembersAsync(request.ChatId, cancellationToken);
}
