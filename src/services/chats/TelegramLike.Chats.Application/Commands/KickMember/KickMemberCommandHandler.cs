using MediatR;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Domain.Repositories;

namespace TelegramLike.Chats.Application.Commands.KickMember;

public sealed class KickMemberCommandHandler(IChatRepository chatRepository, ChatsMetrics metrics)
    : IRequestHandler<KickMemberCommand>
{
    public async Task Handle(KickMemberCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new DomainException("Chat not found.");

        chat.Kick(request.MemberToKickUserId, request.KickedByUserId);
        await chatRepository.UpdateAsync(chat, cancellationToken);
        metrics.RecordMembershipChange("kicked");
    }
}
