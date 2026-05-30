using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Queries;

public sealed record UserPresenceDto(
    Guid UserId,
    OnlineStatus Status,
    DateTime? LastSeenAt,
    bool HideLastSeen);

public sealed record TypingUsersDto(Guid ChatId, IReadOnlyList<Guid> UserIds);
