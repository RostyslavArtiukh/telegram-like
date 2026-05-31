using MediatR;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

public sealed class MarkMessageAsReadCommandHandler(
    IMessageRepository messageRepository,
    IMessageReadReceiptRepository receiptRepository)
    : IRequestHandler<MarkMessageAsReadCommand>
{
    public async Task Handle(MarkMessageAsReadCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        // Membership check moved to Web BFF (Phase 8 will restore it via local
        // read model). Self-read skip stays here — it's purely about the message.
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
