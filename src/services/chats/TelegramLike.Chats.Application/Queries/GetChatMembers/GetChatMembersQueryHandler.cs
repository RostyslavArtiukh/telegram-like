using MediatR;
using TelegramLike.Chats.Application.Common.Interfaces;

namespace TelegramLike.Chats.Application.Queries.GetChatMembers;

public sealed class GetChatMembersQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetChatMembersQuery, IReadOnlyList<ChatMemberDto>>
{
    public async Task<IReadOnlyList<ChatMemberDto>> Handle(GetChatMembersQuery request, CancellationToken cancellationToken)
    {
        // Only members may enumerate the roster (names/roles). 403 for a non-member.
        if (!await chatQueryService.IsActiveMemberAsync(request.ChatId, request.RequesterId, cancellationToken))
            throw new ForbiddenException("You are not a member of this chat.");

        return await chatQueryService.GetChatMembersAsync(request.ChatId, cancellationToken);
    }
}
