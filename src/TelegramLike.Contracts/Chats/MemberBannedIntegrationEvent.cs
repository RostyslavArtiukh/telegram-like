using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

/// <summary>
/// A member was banned from a chat. Consumed by the Messaging and Presence membership
/// read-models (and the Realtime tracker) to deactivate the member — without it a ban only
/// blocks rejoining, while the banned user could keep sending messages, reacting and typing,
/// because those services decide membership from their own materialized view.
/// <para>
/// Deliberately carries no <c>Recipients</c>: unlike a kick, a ban raises no notification
/// fan-out, so no consumer needs the audience.
/// </para>
/// </summary>
public sealed record MemberBannedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    Guid BannedBy,
    string? Reason) : IIntegrationEvent;
