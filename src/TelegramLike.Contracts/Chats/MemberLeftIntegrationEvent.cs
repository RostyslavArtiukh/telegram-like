using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

[IntegrationEventName("chats.member-left.v1")]
public sealed record MemberLeftIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId) : IIntegrationEvent;
