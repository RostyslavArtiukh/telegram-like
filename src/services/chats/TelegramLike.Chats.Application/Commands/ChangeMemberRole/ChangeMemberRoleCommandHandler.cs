using MediatR;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.ChangeMemberRole;

public sealed class ChangeMemberRoleCommandHandler(IChatRepository chatRepository)
    : IRequestHandler<ChangeMemberRoleCommand>
{
    public async Task Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new DomainException("Chat not found.");

        switch (chat)
        {
            case GroupChat group:
                group.ChangeMemberRole(request.MemberToChangeUserId, request.NewRole, request.ChangedByUserId);
                break;
            case BroadcastChannel broadcast:
                if (request.NewRole == MemberRole.Admin)
                    broadcast.PromoteToAdmin(request.MemberToChangeUserId, request.ChangedByUserId);
                else if (request.NewRole == MemberRole.Viewer)
                    broadcast.DemoteToViewer(request.MemberToChangeUserId, request.ChangedByUserId);
                else
                    throw new DomainException(
                        $"BroadcastChannel supports only Admin/Viewer role changes, got {request.NewRole}.");
                break;
            default:
                throw new DomainException("This chat type does not support role changes.");
        }

        await chatRepository.UpdateAsync(chat, cancellationToken);
    }
}
