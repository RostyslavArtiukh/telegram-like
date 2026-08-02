using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Presence;

[IntegrationEventName("presence.user-went-offline.v1")]
public sealed record UserWentOfflineIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId) : IIntegrationEvent;
