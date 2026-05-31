using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

public sealed record MessageRetractedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid RetractedBy) : IIntegrationEvent;
