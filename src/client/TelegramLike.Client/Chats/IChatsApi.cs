namespace TelegramLike.Client.Chats;

public interface IChatsApi
{
    Task<IReadOnlyList<ChatSummary>> GetMyChatsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ChatDetails?> GetChatByIdAsync(Guid actingUserId, Guid chatId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMember>> GetChatMembersAsync(Guid actingUserId, Guid chatId, CancellationToken cancellationToken = default);

    Task<Guid> CreateDirectChatAsync(Guid userId, Guid peerUserId, CancellationToken cancellationToken = default);
    Task<Guid> CreateGroupChatAsync(Guid userId, string name, CancellationToken cancellationToken = default);
    Task<Guid> CreateBroadcastChannelAsync(Guid userId, string name, CancellationToken cancellationToken = default);

    Task JoinChatAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);
    Task LeaveChatAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);
    Task KickMemberAsync(Guid actorUserId, Guid chatId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task ChangeMemberRoleAsync(Guid actorUserId, Guid chatId, Guid targetUserId, MemberRole newRole, CancellationToken cancellationToken = default);
    Task TransferOwnershipAsync(Guid currentOwnerUserId, Guid chatId, Guid newOwnerUserId, CancellationToken cancellationToken = default);
    Task RenameChatAsync(Guid actorUserId, Guid chatId, string newName, CancellationToken cancellationToken = default);

    // Enrichment helpers used to build Messaging commands without exposing
    // Chats internals (membership/role) to the Messaging service.
    Task<IReadOnlyList<Guid>> GetActiveRecipientsAsync(Guid actingUserId, Guid chatId, Guid excludeUserId, CancellationToken cancellationToken = default);
    Task<ChatType?> GetChatTypeAsync(Guid actingUserId, Guid chatId, CancellationToken cancellationToken = default);
    Task<bool> IsModeratorAsync(Guid actingUserId, Guid chatId, Guid userId, CancellationToken cancellationToken = default);
}
