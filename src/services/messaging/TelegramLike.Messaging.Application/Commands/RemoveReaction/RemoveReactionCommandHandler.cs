using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Messaging.Application.Common;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RemoveReaction;

public sealed class RemoveReactionCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership,
    ILogger<RemoveReactionCommandHandler> logger)
    : IRequestHandler<RemoveReactionCommand>
{
    public async Task Handle(RemoveReactionCommand request, CancellationToken cancellationToken)
    {
        await ConcurrencyRetry.ExecuteAsync(async () =>
        {
            var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                          ?? throw new InvalidOperationException("Message not found.");

            var isMember = await membership.IsActiveMemberAsync(message.ChatId, request.UserId, cancellationToken);
            if (!isMember)
            {
                logger.LogWarning(
                    "RemoveReaction: user {UserId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                    request.UserId,
                    message.ChatId);
            }

            message.RemoveReaction(request.UserId, request.Emoji);
            await messageRepository.UpdateAsync(message, cancellationToken);
        });
    }
}
