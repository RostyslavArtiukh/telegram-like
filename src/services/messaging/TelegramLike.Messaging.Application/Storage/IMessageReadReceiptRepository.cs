namespace TelegramLike.Messaging.Application.Storage;

public interface IMessageReadReceiptRepository
{
    // Returns true only when a receipt was newly created for this (message, reader).
    // A repeat read returns false, which lets the broadcast counter increment exactly
    // once per reader. The unique (MessageId, MemberId) index makes this race-safe.
    Task<bool> MarkAsReadAsync(Guid messageId, Guid memberId, DateTime readAt, CancellationToken cancellationToken = default);

    Task<bool> HasReceiptAsync(Guid messageId, Guid memberId, CancellationToken cancellationToken = default);
}
