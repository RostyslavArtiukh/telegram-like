using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

[IntegrationEventName("chats.member-joined.v1")]
public sealed record MemberJoinedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    IReadOnlyList<Guid> Recipients,
    // Additive (nullable, trailing): the member's initial role ("Owner"/"Admin"/
    // "Member"/"Viewer"). Lets consumers materialize role without calling Chats.
    // Old messages without it deserialize to null → treated as a non-moderator.
    string? Role = null,
    // Recipients is this part's slice of the audience, not the whole chat ([TL-124]).
    // The membership read-models care about UserId, which every part repeats, so they can
    // ignore parts entirely; only a per-message action would need PartIndex == 0.
    int PartIndex = 0,
    int PartCount = 1) : IIntegrationEvent;
