using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

/// <summary>
/// One-time backfill of a chat's current active membership into the Messaging and Presence
/// read-models. Emitted once per chat by the Chats admin backfill so chats that predate those
/// read-models become materialized (live memberships already flow as MemberJoined/Left/…).
/// <para>
/// This is a DEDICATED event on purpose: republishing <see cref="MemberJoinedIntegrationEvent"/>
/// would also reach the notifications and realtime consumers and spam users with historical
/// "joined" side effects. Only the two membership read-models subscribe to this event.
/// </para>
/// <para>
/// Consumers apply each entry last-writer-wins by its own <see cref="ChatMembershipSnapshotEntry.JoinedAt"/>
/// (not the event's <see cref="OccurredAt"/>), so a live membership change that already happened
/// always wins over the historical snapshot — the backfill can never resurrect or role-revert.
/// </para>
/// <para>
/// A big chat's snapshot is split into parts ([TL-124]) — <see cref="Members"/> is this part's
/// slice. Every consumer applies entries one at a time, so parts need no coordination; the one
/// consequence worth knowing is that Realtime's tracker treats a chat as "known" from the first
/// part, so members carried by a later part are briefly not recognised.
/// </para>
/// </summary>
[IntegrationEventName("chats.chat-memberships-snapshot.v1")]
public sealed record ChatMembershipsSnapshotIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    IReadOnlyList<ChatMembershipSnapshotEntry> Members,
    int PartIndex = 0,
    int PartCount = 1) : IIntegrationEvent;

/// <summary>One active member in a <see cref="ChatMembershipsSnapshotIntegrationEvent"/>.</summary>
public sealed record ChatMembershipSnapshotEntry(
    Guid UserId,
    string Role,
    DateTime JoinedAt);
