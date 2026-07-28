using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.BanMember;

public sealed class BanMemberCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
    : IRequestHandler<BanMemberCommand>
{
    public async Task Handle(BanMemberCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new DomainException("Chat not found.");

        // Ban lives on GroupChat alone: a BroadcastChannel viewer is kicked (and may return),
        // and a DirectChat has no moderation at all. Blocking someone there is a user-level
        // block in Identity, not a chat-level ban.
        if (chat is not GroupChat group)
            throw new DomainException("This chat type does not support banning.");

        group.Ban(request.MemberToBanUserId, request.BannedByUserId, request.Reason);
        await chatRepository.UpdateAsync(chat, cancellationToken);
        metrics.RecordMembershipChange("banned");
    }
}
