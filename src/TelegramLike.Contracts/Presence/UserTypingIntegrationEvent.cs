using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Presence;

[IntegrationEventName("presence.user-typing.v1")]
public sealed record UserTypingIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId) : IIntegrationEvent;
