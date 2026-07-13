namespace TelegramLike.Chats.Application.Backfill;

/// <summary>
/// Enumerates the current active membership straight from the Chats source-of-truth, grouped by
/// chat, for the one-time read-model backfill. Read-only and only used by the admin backfill path.
/// </summary>
public interface IChatMembershipBackfillReader
{
    Task<IReadOnlyList<ChatMembershipSnapshot>> GetActiveMembershipsByChatAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>All active members of one chat, as read from the source-of-truth.</summary>
public sealed record ChatMembershipSnapshot(Guid ChatId, IReadOnlyList<ChatMembershipSnapshotMember> Members);

public sealed record ChatMembershipSnapshotMember(Guid UserId, string Role, DateTime JoinedAt);
