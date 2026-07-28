namespace TelegramLike.Messaging.Application.Storage;

// Local read model populated from Chats integration events
// (MemberJoined / MemberKicked / MemberLeft). Lets Messaging strictly verify
// the actor belongs to the chat without calling the Chats service back.
// Phase 2 dropped this check (fail-open); this restores it via event-driven
// materialized view, following the Step 25 Presence pattern.
public interface IChatMembershipReadModel
{
    Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    // True when the active member's materialized role is Owner or Admin. Lets retract
    // derive moderator authority server-side instead of trusting a client flag.
    Task<bool> IsModeratorAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    // Every active member of the chat. An empty result is ambiguous on its own — it means
    // either "not materialized yet" or "materialized, but nobody is active any more"
    // (every member banned/kicked, or the chat deleted). Pair it with IsChatKnownAsync.
    Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(Guid chatId, CancellationToken cancellationToken = default);

    // True once ANY membership row exists for the chat, active or not. This is what
    // separates "we have never heard of this chat" (the deliberate fail-open window for a
    // just-created chat) from "we know it and nobody may post" — without it, deleting a
    // chat empties its active members and silently flips access back to fail-open.
    Task<bool> IsChatKnownAsync(Guid chatId, CancellationToken cancellationToken = default);

    // occurredAt is the membership event's timestamp. RabbitMQ is at-least-once with no
    // cross-message ordering, so writes are last-writer-wins by occurredAt: a stale
    // MemberJoined redelivered after a MemberLeft must not resurrect the member.
    Task UpsertActiveAsync(Guid chatId, Guid userId, string? role, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken cancellationToken = default);

    // Deactivates the chat's whole membership in one write, for ChatDeleted. Terminal:
    // a deleted chat can never be rejoined, so nothing may reactivate these rows.
    Task DeactivateChatAsync(Guid chatId, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task SetRoleAsync(Guid chatId, Guid userId, string role, DateTime occurredAt, CancellationToken cancellationToken = default);
}
