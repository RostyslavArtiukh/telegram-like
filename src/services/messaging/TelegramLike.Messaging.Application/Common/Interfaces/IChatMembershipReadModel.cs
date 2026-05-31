namespace TelegramLike.Messaging.Application.Common.Interfaces;

// Local read model populated from Chats integration events
// (MemberJoined / MemberKicked / MemberLeft). Lets Messaging strictly verify
// the actor belongs to the chat without calling the Chats service back.
// Phase 2 dropped this check (fail-open); this restores it via event-driven
// materialized view, following the Step 25 Presence pattern.
public interface IChatMembershipReadModel
{
    Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    Task UpsertActiveAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    Task RemoveAsync(Guid chatId, Guid userId, CancellationToken ct = default);
}
