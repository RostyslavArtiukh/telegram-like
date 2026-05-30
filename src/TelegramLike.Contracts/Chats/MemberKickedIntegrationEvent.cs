using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

public sealed record MemberKickedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    Guid KickedBy,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent;
