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

        // Every chat type records a per-reader receipt; the unique (MessageId, MemberId)
        // index makes repeat/concurrent reads idempotent. For broadcast, bump the stored
        // counter exactly once per reader via an atomic $inc — so N reads (or concurrent
        // reads) can't over-count, and the whole-doc ReplaceOne lost-update is gone.
        var newlyRead = await receiptRepository.MarkAsReadAsync(
            message.Id, request.ReaderUserId, DateTime.UtcNow, cancellationToken);

        if (request.IsBroadcast && newlyRead)
            await messageRepository.IncrementBroadcastReadCountAsync(message.Id, cancellationToken);
    }
}
