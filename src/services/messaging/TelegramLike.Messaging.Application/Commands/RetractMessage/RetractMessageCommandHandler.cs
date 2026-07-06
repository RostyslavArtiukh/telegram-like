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
                          ?? throw new InvalidOperationException("Message not found.");

            var isMember = await membership.IsActiveMemberAsync(message.ChatId, request.ActorUserId, cancellationToken);
            if (!isMember)
            {
                logger.LogWarning(
                    "RetractMessage: actor {ActorUserId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                    request.ActorUserId,
                    message.ChatId);
            }

            var isAuthor = message.AuthorId == request.ActorUserId;

            // Author can always retract own. Moderator (Owner/Admin per chat role)
            // can retract anyone's — flag comes from the Web BFF since Messaging
            // tracks membership but not role in its local read-model.
            message.Retract(request.ActorUserId, isAuthor || request.ActorIsModerator);
            await messageRepository.UpdateAsync(message, cancellationToken);
        });
    }
}
