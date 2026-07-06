using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

public sealed record MemberJoinedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    IReadOnlyList<Guid> Recipients,
    // Additive (nullable, trailing): the member's initial role ("Owner"/"Admin"/
    // "Member"/"Viewer"). Lets consumers materialize role without calling Chats.
    // Old messages without it deserialize to null → treated as a non-moderator.
    string? Role = null) : IIntegrationEvent;
