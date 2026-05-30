using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

public sealed record MemberJoinedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent;
