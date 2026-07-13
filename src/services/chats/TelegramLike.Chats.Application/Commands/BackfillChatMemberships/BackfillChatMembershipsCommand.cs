using MediatR;

namespace TelegramLike.Chats.Application.Commands.BackfillChatMemberships;

/// <summary>
/// One-time operation: publishes a <c>ChatMembershipsSnapshotIntegrationEvent</c> per chat so the
/// Messaging and Presence membership read-models materialize chats that predate them. Idempotent —
/// consumers apply snapshots last-writer-wins by JoinedAt, so re-running is safe.
/// </summary>
public sealed record BackfillChatMembershipsCommand : IRequest<BackfillChatMembershipsResult>;

public sealed record BackfillChatMembershipsResult(int ChatsPublished, int MembersPublished);
