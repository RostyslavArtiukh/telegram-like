using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Presence;

public sealed record UserCameOnlineIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId) : IIntegrationEvent;
