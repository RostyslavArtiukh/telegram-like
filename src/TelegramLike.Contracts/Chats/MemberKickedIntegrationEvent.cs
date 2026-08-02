using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

[IntegrationEventName("chats.member-kicked.v1")]
public sealed record MemberKickedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    Guid KickedBy,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent;
