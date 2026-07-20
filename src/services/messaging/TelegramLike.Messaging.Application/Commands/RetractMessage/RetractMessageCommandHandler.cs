using MediatR;
using TelegramLike.Messaging.Application;
using TelegramLike.Messaging.Application.Observability;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RetractMessage;

public sealed class RetractMessageCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership,
    MessagingMetrics metrics)
    : IRequestHandler<RetractMessageCommand>
{
    public async Task Handle(RetractMessageCommand request, CancellationToken cancellationToken)
    {
        var retractedByModerator = false;

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

            retractedByModerator = isModerator && !isAuthor;
        });

        // Counted outside the retry: the lambda re-runs on a version conflict, and
        // counting in there would report retries as extra retractions.
        metrics.RecordMessageRetracted(retractedByModerator);
    }
}
