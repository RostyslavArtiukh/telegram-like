using TelegramLike.Chats.Application.Queries;

namespace TelegramLike.Chats.Application.Queries;

public interface IChatQueryService
{
    Task<IReadOnlyList<ChatSummaryDto>> GetMyChatsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ChatDetailsDto?> GetChatByIdAsync(Guid chatId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMemberDto>> GetChatMembersAsync(Guid chatId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
}
