using TelegramLike.Messaging.Application.Queries;

namespace TelegramLike.Messaging.Application.Common.Interfaces;

public interface IMessageQueryService
{
    Task<MessagePageDto> GetChatMessagesAsync(
        Guid chatId,
        Guid requesterId,
        DateTime? beforeSentAt,
        int pageSize,
        CancellationToken ct = default);

    Task<MessageDto?> GetMessageByIdAsync(Guid messageId, Guid requesterId, CancellationToken ct = default);
}
