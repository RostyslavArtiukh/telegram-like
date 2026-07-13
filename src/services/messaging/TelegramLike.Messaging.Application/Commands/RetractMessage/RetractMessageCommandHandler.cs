using MediatR;
using TelegramLike.Messaging.Application;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RetractMessage;

public sealed class RetractMessageCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership)
    : IRequestHandler<RetractMessageCommand>
{
    public async Task Handle(RetractMessageCommand request, CancellationToken cancellationToken)
    {
        await ConcurrencyRetry.ExecuteAsync(async () =>
        {
            var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                          ?? throw new DomainException("Message not found.");

            // Fail-closed ([TL-101]): backfilled read-model makes a non-member authoritative.
            if (!await membership.IsActiveMemberAsync(message.ChatId, request.RetractedByUserId, cancellationToken))
                throw new ForbiddenException("You are not a member of this chat.");

            var isAuthor = message.AuthorId == request.RetractedByUserId;

            // Moderator authority is now derived server-side from the materialized role
            // read-model (Owner/Admin), NOT the client-supplied RetractedByModerator flag —
            // external clients bypass the BFF and could otherwise spoof it to retract
            // anyone's message. The flag is ignored (kept on the wire for compatibility).
            var isModerator = await membership.IsModeratorAsync(
                message.ChatId, request.RetractedByUserId, cancellationToken);

            message.Retract(request.RetractedByUserId, isAuthor || isModerator);
            await messageRepository.UpdateAsync(message, cancellationToken);
        });
    }
}
