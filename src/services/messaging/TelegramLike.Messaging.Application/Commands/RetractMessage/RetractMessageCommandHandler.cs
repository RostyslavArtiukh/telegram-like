using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Messaging.Application.Common;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RetractMessage;

public sealed class RetractMessageCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership,
    ILogger<RetractMessageCommandHandler> logger)
    : IRequestHandler<RetractMessageCommand>
{
    public async Task Handle(RetractMessageCommand request, CancellationToken cancellationToken)
    {
        await ConcurrencyRetry.ExecuteAsync(async () =>
        {
            var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                          ?? throw new DomainException("Message not found.");

            var isMember = await membership.IsActiveMemberAsync(message.ChatId, request.ActorUserId, cancellationToken);
            if (!isMember)
            {
                logger.LogWarning(
                    "RetractMessage: actor {ActorUserId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                    request.ActorUserId,
                    message.ChatId);
            }

            var isAuthor = message.AuthorId == request.ActorUserId;

            // Moderator authority is now derived server-side from the materialized role
            // read-model (Owner/Admin), NOT the client-supplied ActorIsModerator flag —
            // external clients bypass the BFF and could otherwise spoof it to retract
            // anyone's message. The flag is ignored (kept on the wire for compatibility).
            var isModerator = await membership.IsModeratorAsync(
                message.ChatId, request.ActorUserId, cancellationToken);

            message.Retract(request.ActorUserId, isAuthor || isModerator);
            await messageRepository.UpdateAsync(message, cancellationToken);
        });
    }
}
