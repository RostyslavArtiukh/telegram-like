using MediatR;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Application.Queries.GetChatMessages;

public sealed class GetChatMessagesQueryHandler(
    IMessageQueryService messageQueryService,
    IChatMembershipReadModel membership)
    : IRequestHandler<GetChatMessagesQuery, MessagePageDto>
{
    public async Task<MessagePageDto> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize is < 1 or > 200 ? 50 : request.PageSize;

        // Enforce membership here, not only in the BFF: external SDK/MAUI clients reach
        // Messaging directly through the gateway, so a "the BFF checks first" assumption
        // is not a control. Fail closed when the chat is materialized and the requester
        // isn't a member; fall through for an unknown chat (same fail-open window as
        // SendMessage, e.g. a MemberJoined still in flight).
        var activeMembers = await membership.GetActiveMemberIdsAsync(request.ChatId, cancellationToken);
        var chatKnown = activeMembers.Count > 0
                        || await membership.IsChatKnownAsync(request.ChatId, cancellationToken);

        if (chatKnown && !activeMembers.Contains(request.RequesterId))
            throw new ForbiddenException("You are not an active member of this chat.");

        return await messageQueryService.GetChatMessagesAsync(
            request.ChatId,
            request.RequesterId,
            request.BeforeSentAt,
            pageSize,
            cancellationToken);
    }
}
