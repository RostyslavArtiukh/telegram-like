using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Presence;

public sealed record UserTypingIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId) : IIntegrationEvent;
