using MediatR;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

public sealed class MarkMessageAsReadCommandHandler(
    IMessageRepository messageRepository,
    IMessageReadReceiptRepository receiptRepository,
    IChatMembershipReadModel membership)
    : IRequestHandler<MarkMessageAsReadCommand>
{
    public async Task Handle(MarkMessageAsReadCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new DomainException("Message not found.");

        // Fail-closed ([TL-101]): backfilled read-model makes a non-member authoritative.
        if (!await membership.IsActiveMemberAsync(message.ChatId, request.ReaderUserId, cancellationToken))
            throw new ForbiddenException("You are not a member of this chat.");

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
