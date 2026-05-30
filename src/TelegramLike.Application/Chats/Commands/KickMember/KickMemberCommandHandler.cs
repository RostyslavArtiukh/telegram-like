using MediatR;
using TelegramLike.Domain.Chats.Repositories;

namespace TelegramLike.Application.Chats.Commands.KickMember;

public sealed class KickMemberCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<KickMemberCommand>
{
    public async Task Handle(KickMemberCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        chat.Kick(request.TargetUserId, request.ActorUserId);
        await chatRepository.UpdateAsync(chat, cancellationToken);
    }
}
