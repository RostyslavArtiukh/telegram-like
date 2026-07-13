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

/// <summary>
/// All active members of one chat, plus the chat's type, as read from the source-of-truth.
/// <see cref="ChatType"/> is the <c>ChatType</c> name; it drives the chat-type backfill.
/// </summary>
public sealed record ChatMembershipSnapshot(Guid ChatId, string ChatType, IReadOnlyList<ChatMembershipSnapshotMember> Members);

public sealed record ChatMembershipSnapshotMember(Guid UserId, string Role, DateTime JoinedAt);
