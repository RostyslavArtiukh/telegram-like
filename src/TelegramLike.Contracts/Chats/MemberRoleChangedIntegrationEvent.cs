using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

// Published by Chats when a member's role changes (promote/demote/ownership transfer).
// Lets consumers keep a materialized role read-model current without querying Chats —
// e.g. Messaging derives moderator authority for retract from it instead of trusting
// a client-supplied flag. Role is "Owner"/"Admin"/"Member"/"Viewer".
[IntegrationEventName("chats.member-role-changed.v1")]
public sealed record MemberRoleChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    string Role) : IIntegrationEvent;
