using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

/// <summary>
/// A chat was deleted by its owner. Consumed by the Messaging and Presence membership
/// read-models (and the Realtime tracker) to deactivate the chat's whole membership at once.
/// Chats itself refuses further operations via its own <c>DeletedAt</c>, but those services
/// decide membership from their materialized view — without this event they would keep
/// accepting messages, reactions and typing in a chat that no longer exists.
/// <para>
/// Terminal by nature: a deleted chat can never be rejoined, so consumers treat the
/// deactivation as final rather than something a later event might reverse.
/// </para>
/// </summary>
[IntegrationEventName("chats.chat-deleted.v1")]
public sealed record ChatDeletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid DeletedBy) : IIntegrationEvent;
