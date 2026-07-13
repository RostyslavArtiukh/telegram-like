namespace TelegramLike.Messaging.Application.Storage;

/// <summary>
/// Local read-model of each chat's immutable type, materialized from the Chats
/// <c>ChatCreatedIntegrationEvent</c> (plus the one-time backfill). Lets SendMessage derive
/// <c>isBroadcast</c> server-side ([TL-102]) instead of trusting a client-supplied flag, without
/// cross-querying Chats. Chat type never changes, so writes are a plain set-once upsert.
/// </summary>
public interface IChatTypeReadModel
{
    /// <summary>
    /// True if the chat is a broadcast channel, false if Direct/Group, null if the chat is not
    /// materialized yet (legacy chat pre-backfill, or a just-created chat's event still in flight).
    /// </summary>
    Task<bool?> IsBroadcastAsync(Guid chatId, CancellationToken cancellationToken = default);

    Task UpsertAsync(Guid chatId, string chatType, CancellationToken cancellationToken = default);
}
