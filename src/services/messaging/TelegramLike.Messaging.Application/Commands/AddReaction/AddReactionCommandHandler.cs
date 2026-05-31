using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.AddReaction;

public sealed class AddReactionCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership,
    ILogger<AddReactionCommandHandler> logger)
    : IRequestHandler<AddReactionCommand>
{
    public async Task Handle(AddReactionCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        var isMember = await membership.IsActiveMemberAsync(message.ChatId, request.UserId, cancellationToken);
        if (!isMember)
        {
            logger.LogWarning(
                "AddReaction: user {UserId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                request.UserId,
                message.ChatId);
        }

        message.AddReaction(request.UserId, request.Emoji, request.ActorIsPremium);
        await messageRepository.UpdateAsync(message, cancellationToken);
    }
}
