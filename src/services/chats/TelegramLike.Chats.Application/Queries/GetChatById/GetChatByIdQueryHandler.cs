using MediatR;
using TelegramLike.Chats.Application.Queries;

namespace TelegramLike.Chats.Application.Queries.GetChatById;

public sealed class GetChatByIdQueryHandler(IChatQueryService chatQueryService)
    : IRequestHandler<GetChatByIdQuery, ChatDetailsDto?>
{
    public async Task<ChatDetailsDto?> Handle(GetChatByIdQuery request, CancellationToken cancellationToken)
    {
        // Only members may see a chat's details + roster. External clients reach Chats
        // directly through the gateway, so this can't be left to the BFF. Hide as 404
        // (return null) so a non-member can't probe which chat ids exist.
        if (!await chatQueryService.IsActiveMemberAsync(request.ChatId, request.RequesterId, cancellationToken))
            return null;

        return await chatQueryService.GetChatByIdAsync(request.ChatId, cancellationToken);
    }
}
