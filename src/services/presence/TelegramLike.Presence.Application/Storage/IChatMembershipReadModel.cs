namespace TelegramLike.Presence.Application.Storage;

// Local read model populated from Chats integration events
// (MemberJoined / MemberKicked / MemberLeft). Lets the Presence service
// validate chat membership without calling back into the Chats database
// — the cross-context dependency we dropped during the Day 15 extraction.
public interface IChatMembershipReadModel
{
    Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    // occurredAt is the membership event's timestamp. RabbitMQ is at-least-once and
    // gives no cross-message ordering, so writes are last-writer-wins by occurredAt:
    // a stale MemberJoined redelivered after a MemberLeft must not resurrect the row.
    Task UpsertActiveAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid chatId, Guid userId, DateTime occurredAt, CancellationToken cancellationToken = default);
}
