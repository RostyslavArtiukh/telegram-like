namespace TelegramLike.Web.Services.ChatsApi;

public interface IChatsApi
{
    Task<IReadOnlyList<ChatSummaryContract>> GetMyChatsAsync(Guid userId, CancellationToken ct = default);

    Task<ChatDetailsContract?> GetChatByIdAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default);

    Task<IReadOnlyList<ChatMemberContract>> GetChatMembersAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default);

    Task<Guid> CreateDirectChatAsync(Guid userId, Guid peerUserId, CancellationToken ct = default);
    Task<Guid> CreateGroupChatAsync(Guid userId, string name, CancellationToken ct = default);
    Task<Guid> CreateBroadcastChannelAsync(Guid userId, string name, CancellationToken ct = default);

    Task JoinChatAsync(Guid userId, Guid chatId, CancellationToken ct = default);
    Task LeaveChatAsync(Guid userId, Guid chatId, CancellationToken ct = default);
    Task KickMemberAsync(Guid actorUserId, Guid chatId, Guid targetUserId, CancellationToken ct = default);
    Task ChangeMemberRoleAsync(Guid actorUserId, Guid chatId, Guid targetUserId, MemberRoleContract newRole, CancellationToken ct = default);
    Task TransferOwnershipAsync(Guid currentOwnerUserId, Guid chatId, Guid newOwnerUserId, CancellationToken ct = default);
    Task RenameChatAsync(Guid actorUserId, Guid chatId, string newName, CancellationToken ct = default);

    // BFF enrichment helpers used to build Messaging commands without exposing
    // Chats internals (membership/role) to the Messaging service.
    Task<IReadOnlyList<Guid>> GetActiveRecipientsAsync(Guid actingUserId, Guid chatId, Guid excludeUserId, CancellationToken ct = default);
    Task<ChatTypeContract?> GetChatTypeAsync(Guid actingUserId, Guid chatId, CancellationToken ct = default);
    Task<bool> IsModeratorAsync(Guid actingUserId, Guid chatId, Guid userId, CancellationToken ct = default);
}
