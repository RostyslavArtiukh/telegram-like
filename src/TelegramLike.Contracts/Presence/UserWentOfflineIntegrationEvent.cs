using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Presence;

public sealed record UserWentOfflineIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId) : IIntegrationEvent;
