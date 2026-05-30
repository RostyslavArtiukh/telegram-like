using TelegramLike.Domain.Presence.ValueObjects;

namespace TelegramLike.Application.Presence.Queries;

public sealed record UserPresenceDto(
    Guid UserId,
    OnlineStatus Status,
    DateTime? LastSeenAt,
    bool HideLastSeen);

public sealed record TypingUsersDto(Guid ChatId, IReadOnlyList<Guid> UserIds);
