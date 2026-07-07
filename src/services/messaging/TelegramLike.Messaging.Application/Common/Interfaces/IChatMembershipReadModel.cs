namespace TelegramLike.Messaging.Application.Common.Interfaces;

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

    // Every active member of the chat. An empty result means the chat isn't
    // materialized yet (legacy chat, or a MemberJoined still in flight) — callers
    // treat that as "unknown" and fall back rather than fail closed.
    Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(Guid chatId, CancellationToken cancellationToken = default);

    // occurredAt is the membership event's timestamp. RabbitMQ is at-least-once with no
    // cross-message ordering, so writes are last-writer-wins by occurredAt: a stale
    // MemberJoined redelivered after a MemberLeft must not resurrect the member.
    Task UpsertActiveAsync(Guid chatId, Guid userId, string? role, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task SetRoleAsync(Guid chatId, Guid userId, string role, DateTime occurredAt, CancellationToken cancellationToken = default);
}
