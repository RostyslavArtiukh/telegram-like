using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

public sealed class MarkMessageAsReadCommandHandler(
    IMessageRepository messageRepository,
    IMessageReadReceiptRepository receiptRepository,
    IChatMembershipReadModel membership,
    ILogger<MarkMessageAsReadCommandHandler> logger)
    : IRequestHandler<MarkMessageAsReadCommand>
{
    public async Task Handle(MarkMessageAsReadCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        var isMember = await membership.IsActiveMemberAsync(message.ChatId, request.ReaderUserId, cancellationToken);
        if (!isMember)
        {
            logger.LogWarning(
                "MarkMessageAsRead: reader {ReaderUserId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                request.ReaderUserId,
                message.ChatId);
        }

        // Self-read skip stays here — it's purely about the message, not membership.
        if (message.AuthorId == request.ReaderUserId)
            return;

        if (request.IsBroadcast)
        {
            message.IncrementBroadcastReadCount();
            await messageRepository.UpdateAsync(message, cancellationToken);
            return;
        }

        if (await receiptRepository.HasReceiptAsync(message.Id, request.ReaderUserId, cancellationToken))
            return;

        await receiptRepository.MarkAsReadAsync(message.Id, request.ReaderUserId, DateTime.UtcNow, cancellationToken);
    }
}
