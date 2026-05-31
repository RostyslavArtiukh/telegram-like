namespace TelegramLike.Presence.Application.Abstractions;

// Local read model populated from Chats integration events
// (MemberJoined / MemberKicked / MemberLeft). Lets the Presence service
// validate chat membership without calling back into the Chats database
// — the cross-context dependency we dropped during the Day 15 extraction.
public interface IChatMembershipReadModel
{
    Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    Task UpsertActiveAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    Task RemoveAsync(Guid chatId, Guid userId, CancellationToken ct = default);
}
