namespace TelegramLike.Messaging.Application.Common.Interfaces;

public interface IMessageReadReceiptRepository
{
    Task MarkAsReadAsync(Guid messageId, Guid memberId, DateTime readAt, CancellationToken ct = default);

    Task<bool> HasReceiptAsync(Guid messageId, Guid memberId, CancellationToken ct = default);
}
