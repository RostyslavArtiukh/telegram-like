using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Presence;

[IntegrationEventName("presence.user-came-online.v1")]
public sealed record UserCameOnlineIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId) : IIntegrationEvent;
