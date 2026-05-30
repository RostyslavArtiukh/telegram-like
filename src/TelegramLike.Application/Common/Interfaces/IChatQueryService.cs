using TelegramLike.Application.Chats.Queries;

namespace TelegramLike.Application.Common.Interfaces;

public interface IChatQueryService
{
    Task<IReadOnlyList<ChatSummaryDto>> GetMyChatsAsync(Guid userId, CancellationToken ct = default);
    Task<ChatDetailsDto?> GetChatByIdAsync(Guid chatId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMemberDto>> GetChatMembersAsync(Guid chatId, CancellationToken ct = default);
}
