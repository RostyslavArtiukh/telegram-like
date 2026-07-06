using MediatR;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Application.Queries.GetMessageById;

public sealed class GetMessageByIdQueryHandler(
    IMessageQueryService messageQueryService,
    IChatMembershipReadModel membership)
    : IRequestHandler<GetMessageByIdQuery, MessageDto?>
{
    public async Task<MessageDto?> Handle(GetMessageByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await messageQueryService.GetMessageByIdAsync(
            request.MessageId, request.RequesterId, cancellationToken);
        if (dto is null) return null;

        // Membership check (external clients bypass the BFF). Hide as 404 rather than
        // 403 so a non-member can't probe which message ids exist. Unknown chat falls
        // through (fail-open window, mirrors SendMessage / GetChatMessages).
        var activeMembers = await membership.GetActiveMemberIdsAsync(dto.ChatId, cancellationToken);
        if (activeMembers.Count > 0 && !activeMembers.Contains(request.RequesterId))
            return null;

        return dto;
    }
}
