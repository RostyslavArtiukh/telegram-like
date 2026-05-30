using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Messaging.Repositories;

namespace TelegramLike.Application.Messaging.Commands.MarkMessageAsRead;

public sealed class MarkMessageAsReadCommandHandler(
    IMessageRepository messageRepository,
    IChatRepository chatRepository,
    IMessageReadReceiptRepository receiptRepository)
    : IRequestHandler<MarkMessageAsReadCommand>
{
    public async Task Handle(MarkMessageAsReadCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        var chat = await chatRepository.GetByIdAsync(message.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        if (chat.FindActiveMember(request.ReaderUserId) is null)
            throw new InvalidOperationException("Only active chat members can mark messages as read.");

        if (message.AuthorId == request.ReaderUserId)
            return;

        if (chat.Type == ChatType.Broadcast)
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
