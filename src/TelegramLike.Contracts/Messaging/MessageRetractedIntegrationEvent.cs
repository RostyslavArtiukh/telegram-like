using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

[IntegrationEventName("messaging.message-retracted.v1")]
public sealed record MessageRetractedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid RetractedBy) : IIntegrationEvent;
